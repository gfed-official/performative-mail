using System.Net;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.App;

public sealed class LoopbackStack : INetworkStack
{
    private LoopbackLink? _listen;
    private ushort _listenPort;
    private int _maxPlayers;

    public JoinTarget LocalTarget => new("127.0.0.1", _listen is null ? SessionOptions.DefaultPort : _listenPort);

    public ListenResult Listen(ushort port, int maxPlayers)
    {
        if (port == 0)
            throw new ArgumentOutOfRangeException(nameof(port), port, null);
        if (maxPlayers < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, null);
        if (_listen is not null)
            throw new PortUnavailableException(port);

        var pair = new LoopbackTransport();
        _listen = LoopbackLink.OverPipes(pair.A);
        _listenPort = port;
        _maxPlayers = maxPlayers;
        return new ListenResult(_listen, pair.B);
    }

    public IClientLink Connect(JoinTarget target)
    {
        if (_listen is null || target.Port != _listenPort || !IsLoopbackHost(target.Host))
            return new ClosedClientLink(DisconnectReason.Timeout);
        if (_listen.OpenCount >= _maxPlayers)
            return new ClosedClientLink(DisconnectReason.Rejected);

        var pair = new LoopbackTransport();
        var id = _listen.Accept(pair.A);
        return new LoopbackClientLink(_listen, id, pair.B);
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(host, "loopback", StringComparison.OrdinalIgnoreCase))
            return true;
        return IPAddress.TryParse(host, out var ip) && IPAddress.IsLoopback(ip);
    }

    private sealed class ClosedClientLink : IClientLink
    {
        public ClosedClientLink(DisconnectReason reason) => CloseReason = reason;

        public LinkState State => LinkState.Closed;

        public DisconnectReason CloseReason { get; }

        public void Send(int channelId, byte[] payload)
        {
        }

        public bool Poll(out int channelId, out byte[] payload)
        {
            channelId = 0;
            payload = Array.Empty<byte>();
            return false;
        }

        public void Pump()
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class LoopbackClientLink : IClientLink
    {
        private readonly LoopbackLink _server;
        private readonly ConnectionId _id;
        private readonly ITransport _inner;
        private bool _opened;
        private bool _disposed;

        public LoopbackClientLink(LoopbackLink server, ConnectionId id, ITransport inner)
        {
            _server = server;
            _id = id;
            _inner = inner;
        }

        public LinkState State
        {
            get
            {
                if (_disposed || !_server.IsOpen(_id))
                    return LinkState.Closed;
                return _opened ? LinkState.Open : LinkState.Connecting;
            }
        }

        public DisconnectReason CloseReason =>
            State == LinkState.Closed ? DisconnectReason.PeerLeft : default;

        public void Send(int channelId, byte[] payload) => _inner.Send(channelId, payload);

        public bool Poll(out int channelId, out byte[] payload) =>
            _inner.Poll(out channelId, out payload);

        public void Pump()
        {
            if (State != LinkState.Closed)
                _opened = true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_server.IsOpen(_id))
                _server.Close(_id, DisconnectReason.PeerLeft);
        }
    }
}
