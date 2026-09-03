using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Game.Net;

public sealed class EnetClientLink : IClientLink
{
    private readonly ENetConnection _host;
    private readonly ENetPacketPeer _peer;
    private readonly Queue<(int Channel, byte[] Payload)> _outbound = new();
    private readonly Queue<(int Channel, byte[] Payload)> _inbound = new();
    private LinkState _state = LinkState.Connecting;
    private DisconnectReason _closeReason;
    private bool _disposed;

    private EnetClientLink(ENetConnection host, ENetPacketPeer peer)
    {
        _host = host;
        _peer = peer;
        _peer.SetTimeout(32, 2000, 5000);
    }

    public static EnetClientLink Dial(JoinTarget target)
    {
        var host = new ENetConnection();
        var err = host.CreateHost(maxPeers: 1, maxChannels: NetChannels.Count);
        if (err != Error.Ok)
        {
            host.Destroy();
            throw new InvalidOperationException($"ENet client host failed: {err}.");
        }

        var peer = host.ConnectToHost(target.Host, target.Port, channels: NetChannels.Count);
        return new EnetClientLink(host, peer);
    }

    public LinkState State => _state;

    public DisconnectReason CloseReason => _closeReason;

    public void Send(int channelId, byte[] payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));
        if (_state == LinkState.Closed)
            return;
        if (_state != LinkState.Open)
        {
            _outbound.Enqueue((channelId, payload));
            return;
        }

        SendNow(channelId, payload);
    }

    public bool Poll(out int channelId, out byte[] payload)
    {
        Pump();
        if (_inbound.Count == 0)
        {
            channelId = 0;
            payload = Array.Empty<byte>();
            return false;
        }

        (channelId, payload) = _inbound.Dequeue();
        return true;
    }

    public void Pump()
    {
        if (_disposed || _state == LinkState.Closed)
            return;

        while (true)
        {
            var ev = _host.Service(0);
            var type = (ENetConnection.EventType)(int)ev[0];
            if (type == ENetConnection.EventType.None)
                break;

            int channel = (int)ev[3];
            switch (type)
            {
                case ENetConnection.EventType.Connect:
                    _state = LinkState.Open;
                    FlushOutbound();
                    break;
                case ENetConnection.EventType.Receive:
                    _inbound.Enqueue((channel, _peer.GetPacket()));
                    break;
                case ENetConnection.EventType.Disconnect:
                case ENetConnection.EventType.Error:
                    _state = LinkState.Closed;
                    _closeReason = DisconnectReason.PeerLeft;
                    _outbound.Clear();
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        var peerState = _peer.GetState();
        if (_state == LinkState.Connecting && IsOpen(peerState))
        {
            _state = LinkState.Open;
            FlushOutbound();
        }
        else if (_state != LinkState.Closed && IsDead(peerState))
        {
            _state = LinkState.Closed;
            _closeReason = DisconnectReason.Timeout;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _state = LinkState.Closed;
        _peer.PeerDisconnectNow(0);
        _host.Flush();
        _host.Destroy();
    }

    private void FlushOutbound()
    {
        while (_outbound.Count > 0)
        {
            var packet = _outbound.Dequeue();
            SendNow(packet.Channel, packet.Payload);
        }
    }

    private void SendNow(int channelId, byte[] payload) =>
        _peer.Send(channelId, payload, EnetServerLink.FlagsFor(channelId));

    private static bool IsOpen(ENetPacketPeer.PeerState state) =>
        state == ENetPacketPeer.PeerState.Connected;

    private static bool IsDead(ENetPacketPeer.PeerState state) =>
        state is ENetPacketPeer.PeerState.Disconnected
            or ENetPacketPeer.PeerState.Zombie
            or ENetPacketPeer.PeerState.Disconnecting
            or ENetPacketPeer.PeerState.AcknowledgingDisconnect;
}
