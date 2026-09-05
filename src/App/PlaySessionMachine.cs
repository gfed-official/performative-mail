using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public sealed class PlaySessionMachine : IDisposable
{
    private readonly INetworkStack _stack;
    private readonly SessionOptions _options;
    private readonly PawnViewTable _pawns = new();
    private readonly RenderClock _clock = new();
    private TickPacer _pacer = TickPacer.AtTickRate();
    private PlaySession _state = PlaySession.Menu.Instance;
    private Live _live = Live.None.Instance;
    private ContentStackCatalog? _catalog;

    public PlaySessionMachine(INetworkStack stack, SessionOptions? options = null)
    {
        _stack = stack ?? throw new ArgumentNullException(nameof(stack));
        _options = options ?? SessionOptions.Default;
    }

    public PlaySession State => _state;

    public bool ClockPaused { get; private set; }

    public bool TrySetClockPaused(bool paused)
    {
        if (!paused)
        {
            ClockPaused = false;
            return true;
        }

        if (_live.Server is not { } server || server.JoinedCount != 1)
            return false;

        ClockPaused = true;
        return true;
    }

    public void Host() => StartHost(ArcadeSession.Create);

    public void HostDebug() => StartHost(ArcadeSession.CreateDebug);

    private void StartHost(Func<ArcadeBoot> create)
    {
        Leave();
        try
        {
            var boot = create();
            var listen = _stack.Listen(_options.ListenPort, _options.MaxPlayers);
            var server = new ServerRuntime(listen.Link, boot);
            server.Start();
            var client = new ClientRuntime(Stacks());
            client.Connect(listen.HostSeat);
            var role = new SessionRole.Listening(HostAdvertisement.For(_options.ListenPort));
            _live = new Live.Hosting(server, listen.Link, client, role);
            _state = new PlaySession.Connecting(role, Deadline: null);
        }
        catch (PortUnavailableException)
        {
            _state = new PlaySession.Failed(new FailReason.PortInUse(_options.ListenPort));
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            _state = new PlaySession.Failed(new FailReason.BootFailed(ex.Message));
        }
    }

    public void Join(JoinTarget target)
    {
        Leave();
        try
        {
            var link = _stack.Connect(target);
            var client = new ClientRuntime(Stacks());
            client.Connect(link);
            var role = new SessionRole.Guest(target);
            _live = new Live.Dialing(link, client, role);
            _state = new PlaySession.Connecting(role, Deadline: null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidOperationException)
        {
            _state = new PlaySession.Failed(new FailReason.BootFailed(ex.Message));
        }
    }

    public void Leave()
    {
        ClockPaused = false;
        _live.Dispose();
        _live = Live.None.Instance;
        _pawns.Clear();
        _clock.Reset();
        _pacer = TickPacer.AtTickRate();
        _state = PlaySession.Menu.Instance;
    }

    public PlaySession Pump(TimeSpan wallNow, in MoveIntent intent)
    {
        switch (_state)
        {
            case PlaySession.Menu:
            case PlaySession.Failed:
                return _state;
            case PlaySession.Connecting connecting:
                _state = PumpConnecting(connecting, wallNow);
                return _state;
            case PlaySession.Playing:
                _state = PumpPlaying(wallNow, in intent);
                return _state;
            default:
                throw new ArgumentOutOfRangeException(nameof(_state), _state, null);
        }
    }

    public void Dispose() => Leave();

    public DebugSnapshot Inspect()
    {
        switch (_state)
        {
            case PlaySession.Menu:
                return DebugSnapshot.Idle(DebugConnection.Menu);
            case PlaySession.Failed:
                return DebugSnapshot.Idle(DebugConnection.Failed);
            case PlaySession.Connecting connecting:
                return InspectLive(DebugConnection.Connecting, connecting.Role, canCheat: false);
            case PlaySession.Playing playing:
                return InspectLive(DebugConnection.Playing, playing.Role, canCheat: playing.Role is SessionRole.Listening);
            default:
                throw new ArgumentOutOfRangeException(nameof(_state), _state, null);
        }
    }

    public bool TryGiveWallet(Cents amount)
    {
        if (_state is not PlaySession.Playing)
            return false;
        if (_live.Server is not ServerRuntime server)
            return false;
        if (amount.Value <= 0)
            return false;

        server.World.Wallet.Credit(amount);
        return true;
    }

    public bool TryAdvancePhase()
    {
        if (_state is not PlaySession.Playing)
            return false;
        return _live.Server is ServerRuntime server && server.TryAdvancePhase();
    }

    public bool TryResetLocalPawn()
    {
        if (!TryHostPlaying(out var server, out var local))
            return false;
        if (!server.World.Players.TryGet(local, out var body))
            return false;

        var spawn = SpawnRing.Pose(SpawnRing.CentreOf(server.World.Atlas), body.SpawnSlot);
        return TrySetLocalPose(in spawn);
    }

    public bool TryTeleportToIntake()
    {
        if (!TryHostPlaying(out var server, out _))
            return false;
        if (server.World.Atlas is not WorldAtlas atlas)
            return false;

        return TrySetLocalPose(TileCentre(atlas.PostOffice.IntakeTile, atlas.TileCm));
    }

    public bool TryTeleportToMailbox()
    {
        if (!TryHostPlaying(out var server, out _))
            return false;
        if (server.Tables is not { Houses.Length: > 0 } tables)
            return false;

        var box = tables.Houses[0].Mailbox;
        return TrySetLocalPose(new PlayerPose(box.XCm, box.YCm, box.ZCm, 0));
    }

    public bool TryGiveMail()
    {
        if (!TryHostPlaying(out var server, out var local))
            return false;
        if (server.World.Inventory is not InventorySystem inventory)
            return false;
        if (!TryHotbar(inventory, out var hotbar))
            return false;
        if (TryFirstMail(inventory, hotbar, out _, out _))
            return true;
        if (server.World.Intake.Value != 0 &&
            TryFirstMail(inventory, server.World.Intake, out var intakeEntry, out _) &&
            inventory.Apply(
                Actor.Player(local),
                new QuickMove(server.World.Intake, intakeEntry, hotbar, Amount.Of(1))) is Accepted)
            return true;

        return TrySpawnLetter(server, inventory, hotbar);
    }

    public bool TryStockIntake()
    {
        if (!TryHostPlaying(out var server, out _))
            return false;
        if (server.World.Inventory is not InventorySystem inventory)
            return false;
        if (server.World.Intake.Value == 0)
            return false;
        if (TryFirstMail(inventory, server.World.Intake, out _, out _))
            return true;
        if (server.Tables is not { Houses.Length: > 0 } tables)
            return false;

        return TrySpawnLetter(server, inventory, server.World.Intake, tables.Houses[0].Address);
    }

    private ContentStackCatalog Stacks()
    {
        if (_catalog is not null)
            return _catalog;
        ContentBoot.Load(out _, out _catalog);
        return _catalog;
    }

    private bool TryHostPlaying(out ServerRuntime server, out EntityId local)
    {
        server = null!;
        local = default;
        if (_state is not PlaySession.Playing)
            return false;
        if (_live.Server is not ServerRuntime runtime)
            return false;
        if (_live.Client.LocalPlayer is not EntityId id)
            return false;

        server = runtime;
        local = id;
        return true;
    }

    private bool TrySetLocalPose(in PlayerPose pose)
    {
        if (!TryHostPlaying(out var server, out var local))
            return false;
        if (!server.World.Players.TryGet(local, out var body))
            return false;

        body.SetPose(pose);
        _live.Client.Prediction.Reconcile(in pose, uint.MaxValue);
        return true;
    }

    private static bool TrySpawnLetter(ServerRuntime server, InventorySystem inventory, ContainerId dest)
    {
        if (server.World.Atlas is not WorldAtlas atlas)
            return false;
        if (atlas.DeliverableAddresses.Count == 0)
            return false;

        return TrySpawnLetter(server, inventory, dest, atlas.DeliverableAddresses[0]);
    }

    private static bool TrySpawnLetter(
        ServerRuntime server,
        InventorySystem inventory,
        ContainerId dest,
        AddressId address)
    {
        if (server.World.Mail is not MailRegistry mail)
            return false;
        if (server.World.Atlas is not WorldAtlas atlas)
            return false;

        var id = mail.Allocate();
        var item = new MailItem(
            id,
            MailKinds.Letter,
            address,
            MailKinds.ValueAtSpawn(MailKinds.Letter, atlas.DistrictId, MailSpawnConstants.Shift1),
            MailSpawnConstants.Shift1,
            MailSpawnConstants.Shift1);
        if (!mail.Register(item))
            return false;
        return inventory.Apply(Actor.System, new Deposit(dest, MailStack.Single(MailKinds.Letter, address, id)))
            is Accepted;
    }

    private static bool TryHotbar(InventorySystem inventory, out ContainerId hotbar)
    {
        foreach (var container in inventory.Containers)
        {
            var shape = container.Spec.Shape;
            if (shape.Cols != 8 || shape.Rows != 1)
                continue;
            hotbar = container.Id;
            return true;
        }

        hotbar = default;
        return false;
    }

    private static bool TryFirstMail(InventorySystem inventory, ContainerId container, out EntryId entry, out MailStack stack)
    {
        entry = default;
        stack = null!;
        if (!inventory.TryGetContainer(container, out var grid))
            return false;
        foreach (var item in grid.Entries)
        {
            if (item.Stack is not MailStack mail)
                continue;
            entry = item.Id;
            stack = mail;
            return true;
        }

        return false;
    }

    private static PlayerPose TileCentre(TileCoord tile, int tileCm) =>
        new(tile.X * tileCm + tileCm / 2, tile.Y * tileCm + tileCm / 2, 0, 0);

    private DebugSnapshot InspectLive(DebugConnection connection, SessionRole role, bool canCheat)
    {
        var client = _live.Client;
        var server = _live.Server;
        uint? local = client.LocalPlayer is EntityId id ? id.Value : null;
        uint? tick = server is not null
            ? server.World.CurrentTick
            : client.LastSnapshot?.ServerTick ?? client.ServerTickEstimate;
        RunPhase? phase = server?.Session.Phase ?? client.AcceptedJoin?.Run.Phase;
        byte? shift = server is not null
            ? server.Session.Shift
            : client.AcceptedJoin?.Run.Shift;
        uint? seed = server?.OfferedSettings.Seed
            ?? client.AcceptedSettings?.Seed
            ?? client.AcceptedJoin?.Seed;
        ulong? hash = server?.OfferedWorld?.WorldHash ?? client.AcceptedWorldHash;
        Cents? wallet = server?.World.Wallet.Balance;
        return new DebugSnapshot(
            connection,
            role is SessionRole.Listening,
            local,
            tick,
            phase,
            shift,
            seed,
            hash,
            wallet,
            canCheat);
    }

    private PlaySession PumpConnecting(PlaySession.Connecting connecting, TimeSpan wallNow)
    {
        switch (_live)
        {
            case Live.Hosting hosting:
                hosting.ServerRuntime.TickOnce();
                hosting.Client.Receive();
                return FinishConnecting(
                    connecting,
                    hosting.Client,
                    hosting.Role,
                    wallNow,
                    opened: true,
                    justOpened: connecting.Deadline is null,
                    link: null);
            case Live.Dialing dialing:
                dialing.Link.Pump();
                bool justOpened = false;
                if (dialing.Link.State == LinkState.Open && !dialing.SawOpen)
                {
                    justOpened = true;
                    dialing.SawOpen = true;
                }

                if (dialing.SawOpen || dialing.Link.State == LinkState.Closed)
                    dialing.Client.Receive();
                return FinishConnecting(
                    connecting,
                    dialing.Client,
                    dialing.Role,
                    wallNow,
                    opened: dialing.SawOpen,
                    justOpened: justOpened,
                    link: dialing.Link);
            default:
                return _state;
        }
    }

    private PlaySession FinishConnecting(
        PlaySession.Connecting connecting,
        ClientRuntime client,
        SessionRole role,
        TimeSpan wallNow,
        bool opened,
        bool justOpened,
        IClientLink? link)
    {
        if (client.LastReject is HelloReject reject)
            return Fail(new FailReason.Rejected(reject.Reason));

        if (link is not null && link.State == LinkState.Closed)
        {
            var target = TargetOf(connecting);
            if (link.CloseReason == DisconnectReason.Rejected || opened)
                return Fail(new FailReason.Refused(target));
            return Fail(new FailReason.Unreachable(target));
        }

        if (client.LocalPlayer is EntityId local)
            return EnterPlaying(role, client, local, wallNow);

        TimeSpan deadline;
        if (!opened)
            deadline = connecting.Deadline ?? wallNow + _options.ConnectDeadline;
        else if (justOpened || connecting.Deadline is null)
            deadline = wallNow + _options.HandshakeDeadline;
        else
            deadline = connecting.Deadline.Value;

        if (wallNow > deadline)
        {
            var target = TargetOf(connecting);
            if (!opened)
                return Fail(new FailReason.Unreachable(target));
            return Fail(new FailReason.HandshakeTimeout(target));
        }

        return new PlaySession.Connecting(role, deadline);
    }

    private JoinTarget TargetOf(PlaySession.Connecting connecting) =>
        connecting.Role is SessionRole.Guest guest
            ? guest.Target
            : new JoinTarget("unknown", _options.ListenPort);

    private PlaySession PumpPlaying(TimeSpan wallNow, in MoveIntent intent)
    {
        var client = _live.Client;
        if (_live is Live.Dialing dialing)
        {
            dialing.Link.Pump();
            if (dialing.Link.State == LinkState.Closed)
                return Fail(new FailReason.HostLost());
        }

        if (ClockPaused && _live.Server is { } hosting && hosting.JoinedCount > 1)
            ClockPaused = false;

        int ticks = _pacer.Advance(wallNow);
        if (ClockPaused)
        {
            _live.Server?.TickOnce(advanceSim: false);
            client.Receive();
            return PresentPlaying(client, _live.Role, wallNow);
        }

        for (int i = 0; i < ticks; i++)
        {
            var cmd = new InputCmd(0, intent.AxisX, intent.AxisY, intent.Yaw, intent.Buttons);
            client.SubmitInput(in cmd);
            client.SendInputs();
            _live.Server?.TickOnce();
            client.Receive();
        }

        return PresentPlaying(client, _live.Role, wallNow);
    }

    private PlaySession EnterPlaying(SessionRole role, ClientRuntime client, EntityId local, TimeSpan wallNow)
    {
        _pacer.Reset(wallNow);
        return PresentPlaying(client, role, wallNow);
    }

    private PlaySession PresentPlaying(ClientRuntime client, SessionRole role, TimeSpan wallNow)
    {
        if (client.LastReject is HelloReject reject)
            return Fail(new FailReason.Rejected(reject.Reason));

        if (client.LastSnapshot is SnapshotPacket snapshot)
            _clock.Anchor(snapshot.ServerTick, wallNow);

        if (_clock.TryNow(wallNow, out var serverTime))
            _pawns.Refresh(client, serverTime);
        else if (client.LocalPlayer is EntityId localOnly &&
                 client.TryPresent(localOnly, TimeSpan.Zero, out _))
            _pawns.Refresh(client, TimeSpan.Zero);

        if (client.LocalPlayer is not EntityId local)
            return Fail(new FailReason.HostLost());

        OverlayReplica? overlay = null;
        if (client.Inventory is InventorySystem inv && LiveOverlay.TryFrom(inv, out var replica))
            overlay = replica;

        return new PlaySession.Playing(
            role,
            local,
            _pawns.Visible,
            ProjectHud(client, local),
            _live.Server?.Tables ?? client.GeneratedWorld,
            overlay);
    }

    private HudSnapshot ProjectHud(ClientRuntime client, EntityId local)
    {
        if (_live.Server is ServerRuntime server)
        {
            var phase = server.Clock?.State.Phase ?? server.Session.Phase;
            byte shift = server.Clock?.State.Shift ?? server.Session.Shift;
            uint now = server.Clock?.Now ?? server.World.CurrentTick;
            uint deadline = server.Clock?.State.PhaseDeadlineTick ?? server.Session.PhaseDeadlineTick;
            InteractPrompt interact = InteractPrompt.None.Instance;
            if (server.TryPickupAddress(local, out var incoming))
                interact = new InteractPrompt.Pickup(incoming);
            else if (server.TryInteractAddresses(local, out var held, out var target))
                interact = new InteractPrompt.Deliver(held, target);
            return new HudSnapshot(
                phase,
                shift,
                now,
                deadline,
                server.World.Wallet.Balance,
                interact,
                server.World.Wallet.Balance,
                server.QuotaFor(shift),
                server.World.Complaint.Points);
        }

        var join = client.AcceptedJoin;
        return new HudSnapshot(
            join?.Run.Phase ?? RunPhase.Lobby,
            join?.Run.Shift ?? 1,
            client.ServerTickEstimate,
            join?.Run.PhaseDeadlineTick ?? 0,
            default,
            InteractPrompt.None.Instance,
            default,
            default,
            0);
    }

    private PlaySession Fail(FailReason reason)
    {
        ClockPaused = false;
        _live.Dispose();
        _live = Live.None.Instance;
        _pawns.Clear();
        _clock.Reset();
        return new PlaySession.Failed(reason);
    }

    private abstract class Live : IDisposable
    {
        private Live()
        {
        }

        public abstract ClientRuntime Client { get; }

        public abstract SessionRole Role { get; }

        public abstract ServerRuntime? Server { get; }

        public abstract void Dispose();

        public sealed class None : Live
        {
            public static None Instance { get; } = new();

            public override ClientRuntime Client =>
                throw new InvalidOperationException("No session is active.");

            public override SessionRole Role =>
                throw new InvalidOperationException("No session is active.");

            public override ServerRuntime? Server => null;

            public override void Dispose()
            {
            }
        }

        public sealed class Hosting : Live
        {
            public Hosting(ServerRuntime server, IServerLink link, ClientRuntime client, SessionRole role)
            {
                ServerRuntime = server;
                Link = link;
                Client = client;
                Role = role;
            }

            public ServerRuntime ServerRuntime { get; }

            public IServerLink Link { get; }

            public override ClientRuntime Client { get; }

            public override SessionRole Role { get; }

            public override ServerRuntime? Server => ServerRuntime;

            public override void Dispose()
            {
                ServerRuntime.Stop();
                Link.Dispose();
            }
        }

        public sealed class Dialing : Live
        {
            public Dialing(IClientLink link, ClientRuntime client, SessionRole role)
            {
                Link = link;
                Client = client;
                Role = role;
            }

            public IClientLink Link { get; }

            public override ClientRuntime Client { get; }

            public override SessionRole Role { get; }

            public override ServerRuntime? Server => null;

            public bool SawOpen { get; set; }

            public override void Dispose() => Link.Dispose();
        }
    }
}
