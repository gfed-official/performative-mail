using System;
using System.Collections.Generic;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Server;

public sealed class ServerRuntime
{
    private readonly IServerLink _link;
    private readonly Dictionary<ConnectionId, Seat> _seats = new();
    private PlayerSnapshot[] _snapshotScratch = Array.Empty<PlayerSnapshot>();
    private uint _tick;

    public SimWorld World { get; }

    public WorldOffer? OfferedWorld { get; }

    public RunSettings OfferedSettings { get; }

    public RunState Session { get; }

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
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        World = world ?? throw new ArgumentNullException(nameof(world));
        OfferedWorld = offeredWorld;
        OfferedSettings = offeredSettings ?? RunSettings.Arcade();
        Session = session ?? RunState.InLobby();
    }

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void TickOnce()
    {
        Drain();
        DropExpired();
        Deaths?.AdvanceTo(_tick);
        World.Tick(_tick++);
        FlushInventoryEvents();

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

        Deaths ??= new DeathSession(inv, inv.CreateContainer(ContainerSpec.Intake), PlayerPose.Origin);
        Deaths.Bind(body, hotbar, inventory, backpack, cursor);
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
            ApplyInputPacket(from, packet);
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
        }
    }

    private void OnClosed(ConnectionId from)
    {
        if (!_seats.TryGetValue(from, out var seat))
            return;

        _seats.Remove(from);
        if (seat.Player is not EntityId player)
            return;

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
}
