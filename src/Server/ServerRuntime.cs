using System;
using System.Collections.Generic;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Server;

public sealed class ServerRuntime
{
    public const int InteractHoldTicks = 12;

    public const int BuildRangeCm = 800;

    private readonly IServerLink _link;
    private readonly Dictionary<ConnectionId, Seat> _seats = new();
    private readonly Dictionary<uint, PlayerBags> _bags = new();
    private PlayerSnapshot[] _snapshotScratch = Array.Empty<PlayerSnapshot>();
    private uint _tick;

    public SimWorld World { get; }

    public WorldOffer? OfferedWorld { get; }

    public RunSettings OfferedSettings { get; }

    public RunState Session { get; private set; }

    public ShiftClock? Clock { get; }

    public WorldTables? Tables { get; }

    public Destinations? Destinations { get; }

    public BalanceTable? Balance { get; }

    public IReadOnlyList<ContainerDelta> LastFlushedDeltas { get; private set; } = Array.Empty<ContainerDelta>();

    public DisconnectGrace Grace { get; } = new();

    public DeathSession? Deaths { get; private set; }

    public bool EndedWithoutResults => Grace.EndedWithoutResults;

    public int JoinedCount
    {
        get
        {
            int n = 0;
            foreach (var seat in _seats.Values)
            {
                if (seat.Joined)
                    n++;
            }

            return n;
        }
    }

    public ServerRuntime(IServerLink link)
        : this(link, new SimWorld())
    {
    }

    public ServerRuntime(IServerLink link, SimWorld world)
        : this(link, world, offeredWorld: null)
    {
    }

    public ServerRuntime(IServerLink link, SimWorld world, WorldOffer? offeredWorld)
        : this(link, world, offeredWorld, offeredSettings: null)
    {
    }

    public ServerRuntime(IServerLink link, SimWorld world, WorldOffer? offeredWorld, RunSettings? offeredSettings)
        : this(link, world, offeredWorld, offeredSettings, session: null)
    {
    }

    public ServerRuntime(
        IServerLink link,
        SimWorld world,
        WorldOffer? offeredWorld,
        RunSettings? offeredSettings,
        RunState? session)
        : this(link, world, offeredWorld, offeredSettings, session, clock: null, tables: null, destinations: null, balance: null)
    {
    }

    public ServerRuntime(IServerLink link, ArcadeBoot boot)
        : this(
            link,
            boot.World,
            boot.Offer,
            boot.Settings,
            boot.Clock.State,
            boot.Clock,
            boot.Tables,
            boot.Destinations,
            boot.Balance)
    {
    }

    private ServerRuntime(
        IServerLink link,
        SimWorld world,
        WorldOffer? offeredWorld,
        RunSettings? offeredSettings,
        RunState? session,
        ShiftClock? clock,
        WorldTables? tables,
        Destinations? destinations,
        BalanceTable? balance)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        World = world ?? throw new ArgumentNullException(nameof(world));
        OfferedWorld = offeredWorld;
        OfferedSettings = offeredSettings ?? RunSettings.Arcade();
        Session = session ?? RunState.InLobby();
        Clock = clock;
        Tables = tables;
        Destinations = destinations;
        Balance = balance;
    }

    public bool TryAdvancePhase()
    {
        if (Clock is ShiftClock clock)
        {
            if (!RunTransitions.TryPrimaryNext(clock.State.Phase, clock.State.Shift, out var next))
                return false;
            if (!clock.TryEnter(next))
                return false;

            Session = clock.State;
            return true;
        }

        if (!RunTransitions.TryPrimaryNext(Session.Phase, Session.Shift, out var fallback))
            return false;
        if (!Session.TryTransition(fallback, out var advanced))
            return false;

        Session = advanced;
        return true;
    }

    public Cents QuotaFor(byte shift)
    {
        if (Balance is null)
            return default;
        int players = Math.Max(1, JoinedCount);
        return QuotaBudget.For(Balance, shift, players).Quota;
    }

    public bool TryPickupAddress(EntityId player, out string address)
    {
        address = "";
        if (!World.Players.TryGet(player, out var body))
            return false;
        if (!CanPickup(body, out var mail))
            return false;

        address = AddressText.Format(mail.Address, Tables?.Streets ?? Array.Empty<StreetRecord>());
        return true;
    }

    public bool TryInteractAddresses(EntityId player, out string held, out string target)
    {
        held = "";
        target = "";
        if (!_bags.TryGetValue(player.Value, out var bags))
            return false;
        if (!World.Players.TryGet(player, out var body))
            return false;
        if (!TryHeldMail(bags.Hotbar, out _, out var mail))
            return false;
        if (!TryNearestMailbox(body, out _, out var house))
            return false;

        var streets = Tables?.Streets ?? Array.Empty<StreetRecord>();
        held = AddressText.Format(mail.Address, streets);
        target = AddressText.Format(house.Address, streets);
        return true;
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void TickOnce(bool advanceSim = true)
    {
        Drain();
        if (!advanceSim)
            return;

        DropExpired();
        Deaths?.AdvanceTo(_tick);
        if (Clock is ShiftClock clock)
        {
            clock.AdvanceTo(_tick);
            Session = clock.State;
        }

        bool spawnMail = Clock is null || Session.Phase == RunPhase.Delivery;
        World.Tick(_tick, spawnMail);
        _tick++;
        FlushInventoryEvents();
        FlushLaneEvents();
        FlushLaneChecksums();

        if (SnapshotCadence.ShouldSend(World.CurrentTick))
            Broadcast();
    }

    public void BindPlayerBags(
        PlayerBody body,
        ContainerId hotbar,
        ContainerId inventory,
        ContainerId? backpack = null,
        ContainerId? cursor = null)
    {
        if (body is null) throw new ArgumentNullException(nameof(body));
        if (World.Inventory is not InventorySystem inv)
            throw new InvalidOperationException("World has no inventory.");

        Deaths ??= new DeathSession(inv, inv.CreateContainer(ContainerSpec.Intake), SpawnRing.CentreOf(World.Atlas));
        Deaths.Bind(body, hotbar, inventory, backpack, cursor);
        _bags[body.Id.Value] = new PlayerBags(hotbar, inventory, 0);
    }

    private void Drain()
    {
        while (_link.TryPoll(out var ev))
        {
            switch (ev.Kind)
            {
                case LinkEventKind.Opened:
                    if (!_seats.ContainsKey(ev.Connection))
                        _seats[ev.Connection] = new Seat(ev.Connection, null, 0);
                    break;
                case LinkEventKind.Data:
                    OnData(ev.Connection, ev.Payload);
                    break;
                case LinkEventKind.Closed:
                    OnClosed(ev.Connection);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ev.Kind), ev.Kind, null);
            }
        }
    }

    private void OnData(ConnectionId from, byte[] payload)
    {
        if (!_seats.ContainsKey(from))
            return;

        if (WireCodec.TryDecode(payload, out AccountHello account))
        {
            OnAccountHello(from, in account);
            return;
        }

        if (WireCodec.TryDecode(payload, out Hello hello))
        {
            HandleHello(from, in hello);
            return;
        }

        if (WireCodec.TryDecode(payload, out Ping ping))
        {
            _link.Send(from, NetChannels.Unreliable, WireCodec.Encode(new Pong(ping.ClientStamp, _tick)));
            return;
        }

        if (WireCodec.TryDecode(payload, out InputPacket? packet) && packet is not null)
        {
            ApplyInputPacket(from, packet);
            return;
        }

        if (ConstructCodec.TryDecode(payload, out PlaceConstructRequest place))
        {
            OnPlaceConstruct(from, in place);
            return;
        }

        if (ConstructCodec.TryDecode(payload, out RemoveConstructRequest remove))
        {
            OnRemoveConstruct(from, in remove);
        }
    }

    private void OnAccountHello(ConnectionId from, in AccountHello hello)
    {
        if (!_seats.TryGetValue(from, out var seat))
            return;

        _seats[from] = new Seat(seat.Id, seat.Player, hello.AccountId);
    }

    private void HandleHello(ConnectionId from, in Hello hello)
    {
        var seat = _seats[from];
        if (seat.Player is not null)
            return;

        if (hello.ProtocolHash != Protocol.Hash)
        {
            _link.Send(from, NetChannels.Handshake, WireCodec.Encode(new HelloReject(HelloRejectReason.ProtocolMismatch)));
            _link.Close(from, DisconnectReason.Rejected);
            return;
        }

        if (seat.Account != 0 && Grace.TryResume(seat.Account, out var resumed))
        {
            Welcome(from, resumed, seat.Account);
            return;
        }

        if (!JoinGate.Allows(Session.Phase))
        {
            _link.Send(from, NetChannels.Handshake, WireCodec.Encode(new HelloReject(HelloRejectReason.WrongPhase)));
            _link.Close(from, DisconnectReason.Rejected);
            return;
        }

        var body = World.SpawnPlayer();
        Welcome(from, body.Id, seat.Account);
        if (Clock is not null)
            EquipPlayer(body);
        BeginShiftIfLobby(body.Id);
    }

    private void Welcome(ConnectionId from, EntityId player, uint account)
    {
        _seats[from] = new Seat(from, player, account);
        _link.Send(from, NetChannels.Handshake, WireCodec.Encode(new HelloOk(player, _tick)));
        _link.Send(from, NetChannels.Handshake, WireCodec.Encode(OfferedSettings));
        if (Session.Phase == RunPhase.Prep)
        {
            _link.Send(from, NetChannels.Handshake, WireCodec.Encode(BuildJoinState()));
            return;
        }

        if (OfferedWorld is WorldOffer offer)
            _link.Send(from, NetChannels.Handshake, WireCodec.Encode(offer));
    }

    private void EquipPlayer(PlayerBody body)
    {
        if (World.Inventory is not InventorySystem inv)
            return;

        var hotbar = inv.CreateContainer(ContainerSpec.Hotbar, body.Id);
        var inventory = inv.CreateContainer(ContainerSpec.BaseInventory, body.Id);
        BindPlayerBags(body, hotbar, inventory);
        inv.Open(body.Id, hotbar);
        inv.Open(body.Id, inventory);
        if (World.Intake.Value != 0)
            inv.Open(body.Id, World.Intake);
    }

    private void BeginShiftIfLobby(EntityId player)
    {
        if (Clock is not ShiftClock clock)
            return;

        clock.Connect(player.Value);
        if (clock.State.Phase != RunPhase.Lobby)
            return;

        clock.TryEnter(RunPhase.Generating);
        clock.TryEnter(RunPhase.Prep);
        Session = clock.State;
    }

    private JoinState BuildJoinState()
    {
        uint seed;
        ulong hash;
        if (OfferedWorld is WorldOffer offer)
        {
            seed = offer.Seed;
            hash = offer.WorldHash;
        }
        else
        {
            seed = OfferedSettings.Seed;
            hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(seed));
        }

        return new JoinState(seed, hash, WorldDeltas.Empty, Session, CollectContainerStamps());
    }

    private ContainerStamp[] CollectContainerStamps()
    {
        if (World.Inventory is not InventorySystem inventory)
            return Array.Empty<ContainerStamp>();

        var stamps = new List<ContainerStamp>();
        foreach (var container in inventory.Containers)
            stamps.Add(new ContainerStamp(container.Id, container.Version, container.Hash));
        return stamps.ToArray();
    }

    private void OnPlaceConstruct(ConnectionId from, in PlaceConstructRequest request)
    {
        if (World.Constructs is not ConstructRegistry constructs)
            return;
        if (!_seats.TryGetValue(from, out var seat) || seat.Player is not EntityId player)
            return;
        if (!World.Players.TryGet(player, out var body))
            return;
        if (!_bags.TryGetValue(player.Value, out var bags))
            return;

        var tile = new TileCoord(request.TileX, request.TileY);
        if (!InBuildRange(body, tile, constructs.TileCm))
            return;

        var result = constructs.TryPlace(
            request.BuildingId,
            tile,
            request.Rotation,
            player,
            bags.Inventory);
        if (result is not Placed placed)
            return;

        BroadcastReliable(ConstructCodec.Encode(new PlaceConstructConfirmed(
            request.ReqId,
            placed.Construct.Id,
            placed.Construct.DefId,
            placed.Construct.Tile.X,
            placed.Construct.Tile.Y,
            placed.Construct.Rotation,
            placed.Construct.Owner)));
    }

    private void OnRemoveConstruct(ConnectionId from, in RemoveConstructRequest request)
    {
        if (World.Constructs is not ConstructRegistry constructs)
            return;
        if (!_seats.TryGetValue(from, out var seat) || seat.Player is not EntityId player)
            return;
        if (!World.Players.TryGet(player, out var body))
            return;
        if (!_bags.TryGetValue(player.Value, out var bags))
            return;
        if (!constructs.TryGet(request.ConstructId, out var row))
            return;
        if (!InBuildRange(body, row.Tile, constructs.TileCm))
            return;

        var result = constructs.TryDeconstruct(request.ConstructId, SalvageRatio(Session.Phase), bags.Inventory);
        if (result is not Deconstructed)
            return;

        BroadcastReliable(ConstructCodec.Encode(new RemoveConstructConfirmed(
            request.ReqId,
            request.ConstructId)));
    }

    private void BroadcastReliable(byte[] payload)
    {
        foreach (var seat in _seats.Values)
        {
            if (!seat.Joined)
                continue;
            _link.Send(seat.Id, NetChannels.Reliable, payload);
        }
    }

    private static double SalvageRatio(RunPhase phase) =>
        phase == RunPhase.Delivery ? 0.5 : 1.0;

    private static bool InBuildRange(PlayerBody body, TileCoord tile, int tileCm)
    {
        int half = tileCm / 2;
        int x = tile.X * tileCm + half;
        int y = tile.Y * tileCm + half;
        return DistSq(body.Xcm, body.Ycm, x, y) <= (long)BuildRangeCm * BuildRangeCm;
    }

    private void ApplyInputPacket(ConnectionId from, InputPacket packet)
    {
        if (!_seats.TryGetValue(from, out var seat) || seat.Player is not EntityId player)
            return;
        if (!World.Players.TryGet(player, out var body))
            return;

        for (int i = packet.Commands.Count - 1; i >= 0; i--)
        {
            var cmd = packet.Commands[i];
            if (cmd.Tick > _tick)
                continue;
            if (body.HasAppliedInput && cmd.Tick <= body.LastProcessedInputTick)
                continue;

            World.ApplyInput(player, in cmd);
            StepInteract(player, body, in cmd);
        }
    }

    private void StepInteract(EntityId player, PlayerBody body, in InputCmd cmd)
    {
        if (!_bags.TryGetValue(player.Value, out var bags))
            return;

        if ((cmd.Buttons & InputButtons.Interact) == 0)
        {
            _bags[player.Value] = bags.ResetHold();
            return;
        }

        if (CanPickup(body, out _))
        {
            if (bags.HoldTicks == 0)
                TryPickup(player, bags.Hotbar);
            _bags[player.Value] = bags.WithHold(1);
            return;
        }

        if (!CanDeliver(body, bags.Hotbar))
        {
            _bags[player.Value] = bags.ResetHold();
            return;
        }

        int held = bags.HoldTicks + 1;
        _bags[player.Value] = bags.WithHold(held);
        if (held != InteractHoldTicks)
            return;

        TryDeliver(player, body, bags.Hotbar);
        _bags[player.Value] = bags.ResetHold();
    }

    private bool CanPickup(PlayerBody body, out MailStack mail)
    {
        mail = null!;
        if (World.Intake.Value == 0)
            return false;
        if (World.Atlas is not WorldAtlas atlas)
            return false;
        if (!NearTile(body, atlas.PostOffice.IntakeTile, atlas.TileCm))
            return false;
        return TryFirstMail(World.Intake, out _, out mail);
    }

    private bool CanDeliver(PlayerBody body, ContainerId hotbar)
        => TryHeldMail(hotbar, out _, out var mail)
           && mail.Ids.Count > 0
           && TryNearestMailbox(body, out _, out _);

    private bool TryPickup(EntityId player, ContainerId hotbar)
    {
        if (World.Inventory is not InventorySystem inv)
            return false;
        if (!TryFirstMail(World.Intake, out var entry, out _))
            return false;

        return inv.Apply(Actor.Player(player), new QuickMove(World.Intake, entry, hotbar, Amount.Of(1))) is Accepted;
    }

    private bool TryDeliver(EntityId player, PlayerBody body, ContainerId hotbar)
    {
        if (Destinations is null)
            return false;
        if (World.Inventory is not InventorySystem inv)
            return false;
        if (!TryHeldMail(hotbar, out var entry, out var mail))
            return false;
        if (!TryNearestMailbox(body, out var dest, out _))
            return false;
        if (mail.Ids.Count == 0)
            return false;

        byte shift = Clock?.State.Shift ?? Session.Shift;
        var result = Destinations.TryDeliver(mail.Ids[0], dest, shift, World.Wallet, World.Complaint);
        switch (result)
        {
            case Delivered:
            case Misdelivered:
                return inv.Apply(Actor.Player(player), new Withdraw(hotbar, entry, Amount.Of(1))) is Accepted;
            case Sim.Mail.Rejected:
                return false;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }

    private bool TryHeldMail(ContainerId hotbar, out EntryId entry, out MailStack mail)
    {
        mail = null!;
        entry = default;
        if (World.Inventory is not InventorySystem inv)
            return false;
        if (!inv.TryGetContainer(hotbar, out var grid))
            return false;
        foreach (var item in grid.Entries)
        {
            if (item.Stack is not MailStack stack)
                continue;
            entry = item.Id;
            mail = stack;
            return true;
        }

        return false;
    }

    private bool TryFirstMail(ContainerId container, out EntryId entry, out MailStack mail)
        => TryHeldMail(container, out entry, out mail);

    private bool TryNearestMailbox(PlayerBody body, out DestinationId dest, out HouseRecord house)
    {
        dest = default;
        house = default;
        if (Tables is null)
            return false;

        long best = long.MaxValue;
        bool found = false;
        var houses = Tables.Houses;
        for (int i = 0; i < houses.Length; i++)
        {
            var candidate = houses[i];
            long dist = DistSq(body.Xcm, body.Ycm, candidate.Mailbox.XCm, candidate.Mailbox.YCm);
            if (dist > (long)WorldAtlasLoader.InteractRangeCm * WorldAtlasLoader.InteractRangeCm)
                continue;
            if (dist >= best)
                continue;
            best = dist;
            house = candidate;
            dest = new DestinationId(candidate.Address.Packed);
            found = true;
        }

        return found;
    }

    private static bool NearTile(PlayerBody body, TileCoord tile, int tileCm)
    {
        int half = tileCm / 2;
        int x = tile.X * tileCm + half;
        int y = tile.Y * tileCm + half;
        return DistSq(body.Xcm, body.Ycm, x, y) <= (long)WorldAtlasLoader.InteractRangeCm * WorldAtlasLoader.InteractRangeCm;
    }

    private static long DistSq(int ax, int ay, int bx, int by)
    {
        long dx = ax - bx;
        long dy = ay - by;
        return dx * dx + dy * dy;
    }

    private void OnClosed(ConnectionId from)
    {
        if (!_seats.TryGetValue(from, out var seat))
            return;

        _seats.Remove(from);
        if (seat.Player is not EntityId player)
            return;

        Clock?.Disconnect(player.Value);
        Grace.Hold(seat.Account, player, _tick, JoinedCount);
    }

    private void DropExpired()
    {
        Grace.AdvanceTo(_tick);
        var expired = Grace.TakeExpired();
        for (int i = 0; i < expired.Count; i++)
            DropHeld(expired[i]);
    }

    private void DropHeld(EntityId player)
    {
        if (!World.Players.TryGet(player, out var body))
            return;

        var tile = new TileCoord(body.Xcm / 100, body.Ycm / 100);
        Deaths?.Drop(player, tile, _tick);
        World.Players.Remove(player);
        _bags.Remove(player.Value);
    }

    private void Broadcast()
    {
        var all = World.Players.All;
        if (_snapshotScratch.Length != all.Count)
            _snapshotScratch = new PlayerSnapshot[all.Count];

        foreach (var seat in _seats.Values)
        {
            if (!seat.Joined)
                continue;

            for (int i = 0; i < all.Count; i++)
            {
                var body = all[i];
                var lastProcessed = seat.Player is EntityId player && body.Id == player
                    ? body.LastProcessedInputTick
                    : 0u;
                _snapshotScratch[i] = new PlayerSnapshot(
                    body.Id,
                    body.Xcm,
                    body.Ycm,
                    body.Zcm,
                    body.Yaw,
                    body.Anim,
                    body.HpPct,
                    lastProcessed,
                    body.VehicleId);
            }

            _link.Send(
                seat.Id,
                NetChannels.Unreliable,
                WireCodec.Encode(new SnapshotPacket(World.CurrentTick, _snapshotScratch)));
        }
    }

    public bool ResendLane(SegmentId segment, byte lane)
    {
        if (lane > 1) return false;
        var segments = World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            var row = segments[i];
            if (!row.Id.Equals(segment)) continue;
            BroadcastReliable(LaneCodec.Encode(row.CaptureState(lane)));
            return true;
        }

        return false;
    }

    private void FlushLaneEvents()
    {
        var deltas = World.Belts.DrainLaneDeltas();
        for (int i = 0; i < deltas.Count; i++)
        {
            switch (deltas[i])
            {
                case LaneInsert insert:
                    BroadcastReliable(LaneCodec.Encode(insert));
                    break;
                case LaneRemove remove:
                    BroadcastReliable(LaneCodec.Encode(remove));
                    break;
            }
        }
    }

    private void FlushLaneChecksums()
    {
        int period = TickClock.TicksFromSeconds(2);
        if (_tick == 0 || _tick % period != 0)
            return;

        var segments = World.Belts.Segments;
        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            for (byte lane = 0; lane < BeltNetwork.LaneCount; lane++)
                BroadcastReliable(LaneCodec.Encode(segment.Checksum(lane)));
        }
    }

    private void FlushInventoryEvents()
    {
        if (World.Inventory is not InventorySystem inventory)
        {
            LastFlushedDeltas = Array.Empty<ContainerDelta>();
            return;
        }

        var deltas = inventory.DrainCommittedDeltas();
        LastFlushedDeltas = deltas;
        for (int i = 0; i < deltas.Count; i++)
        {
            var delta = deltas[i];
            var payload = InventoryCodec.EncodeEvent(delta);
            foreach (var viewer in inventory.ViewersOf(delta.Container))
                SendToViewer(viewer, NetChannels.Reliable, payload);
        }
    }

    private void SendToViewer(EntityId viewer, int channel, byte[] payload)
    {
        foreach (var seat in _seats.Values)
        {
            if (seat.Player is EntityId player && player.Equals(viewer))
            {
                _link.Send(seat.Id, channel, payload);
                return;
            }
        }
    }

    private readonly struct Seat
    {
        public Seat(ConnectionId id, EntityId? player, uint account)
        {
            Id = id;
            Player = player;
            Account = account;
        }

        public ConnectionId Id { get; }

        public EntityId? Player { get; }

        public uint Account { get; }

        public bool Joined => Player.HasValue;
    }

    private readonly struct PlayerBags
    {
        public PlayerBags(ContainerId hotbar, ContainerId inventory, int holdTicks)
        {
            Hotbar = hotbar;
            Inventory = inventory;
            HoldTicks = holdTicks;
        }

        public ContainerId Hotbar { get; }

        public ContainerId Inventory { get; }

        public int HoldTicks { get; }

        public PlayerBags WithHold(int ticks) => new(Hotbar, Inventory, ticks);

        public PlayerBags ResetHold() => new(Hotbar, Inventory, 0);
    }
}
