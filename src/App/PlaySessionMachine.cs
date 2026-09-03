using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

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

    public PlaySessionMachine(INetworkStack stack, SessionOptions? options = null)
    {
        _stack = stack ?? throw new ArgumentNullException(nameof(stack));
        _options = options ?? SessionOptions.Default;
    }

    public PlaySession State => _state;

    public void Host()
    {
        Leave();
        try
        {
            var listen = _stack.Listen(_options.ListenPort, _options.MaxPlayers);
            var server = new ServerRuntime(listen.Link);
            server.Start();
            var client = new ClientRuntime();
            client.Connect(listen.HostSeat);
            var role = new SessionRole.Listening(HostAdvertisement.For(_options.ListenPort));
            _live = new Live.Hosting(server, listen.Link, client, role);
            _state = new PlaySession.Connecting(role, Deadline: null);
        }
        catch (PortUnavailableException)
        {
            _state = new PlaySession.Failed(new FailReason.PortInUse(_options.ListenPort));
        }
    }

    public void Join(JoinTarget target)
    {
        Leave();
        var link = _stack.Connect(target);
        var client = new ClientRuntime();
        client.Connect(link);
        var role = new SessionRole.Guest(target);
        _live = new Live.Dialing(link, client, role);
        _state = new PlaySession.Connecting(role, Deadline: null);
    }

    public void Leave()
    {
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

        int ticks = _pacer.Advance(wallNow);
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
        if (client.LastSnapshot is SnapshotPacket snapshot)
            _clock.Anchor(snapshot.ServerTick, wallNow);

        if (_clock.TryNow(wallNow, out var serverTime))
            _pawns.Refresh(client, serverTime);
        else if (client.LocalPlayer is EntityId localOnly &&
                 client.TryPresent(localOnly, TimeSpan.Zero, out _))
            _pawns.Refresh(client, TimeSpan.Zero);

        if (client.LocalPlayer is not EntityId local)
            return Fail(new FailReason.HostLost());

        return new PlaySession.Playing(role, local, _pawns.Visible);
    }

    private PlaySession Fail(FailReason reason)
    {
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
