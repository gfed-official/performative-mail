using Godot;
using PerformativeMail.App;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Game.Net;

public sealed class EnetServerLink : IServerLink
{
    private readonly ENetConnection _host;
    private readonly Queue<LinkEvent> _events = new();
    private readonly Dictionary<ulong, ConnectionId> _ids = new();
    private readonly Dictionary<uint, ENetPacketPeer> _peers = new();
    private uint _nextId;
    private bool _disposed;

    private EnetServerLink(ENetConnection host, uint firstId)
    {
        _host = host;
        _nextId = firstId;
    }

    public static EnetServerLink Bind(ushort port, int maxPeers, uint firstId)
    {
        var host = new ENetConnection();
        var err = host.CreateHostBound("*", port, maxPeers: maxPeers, maxChannels: NetChannels.Count);
        if (err != Error.Ok)
        {
            host.Destroy();
            throw new PortUnavailableException(port);
        }

        return new EnetServerLink(host, firstId);
    }

    public bool TryPoll(out LinkEvent linkEvent)
    {
        Pump();
        if (_events.Count == 0)
        {
            linkEvent = default;
            return false;
        }

        linkEvent = _events.Dequeue();
        return true;
    }

    public void Send(ConnectionId to, int channelId, byte[] payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));
        if (!_peers.TryGetValue(to.Value, out var peer))
            return;

        peer.Send(channelId, payload, FlagsFor(channelId));
    }

    public void Close(ConnectionId connection, DisconnectReason reason)
    {
        if (!_peers.TryGetValue(connection.Value, out var peer))
            return;

        _host.Flush();
        peer.PeerDisconnectLater(0);
        _ids.Remove(peer.GetInstanceId());
        _peers.Remove(connection.Value);
        _events.Enqueue(LinkEvent.Closed(connection, reason));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var peer in _peers.Values)
            peer.PeerDisconnectNow(0);
        _peers.Clear();
        _ids.Clear();
        _host.Flush();
        _host.Destroy();
    }

    private void Pump()
    {
        while (true)
        {
            var ev = _host.Service(0);
            var type = (ENetConnection.EventType)(int)ev[0];
            if (type == ENetConnection.EventType.None)
                return;

            var peer = ev[1].As<ENetPacketPeer>();
            int channel = (int)ev[3];
            switch (type)
            {
                case ENetConnection.EventType.Connect:
                    OnConnect(peer);
                    break;
                case ENetConnection.EventType.Receive:
                    OnReceive(peer, channel);
                    break;
                case ENetConnection.EventType.Disconnect:
                case ENetConnection.EventType.Error:
                    OnDrop(peer, DisconnectReason.PeerLeft);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

    private void OnConnect(ENetPacketPeer peer)
    {
        var id = new ConnectionId(_nextId++);
        _ids[peer.GetInstanceId()] = id;
        _peers[id.Value] = peer;
        _events.Enqueue(LinkEvent.Opened(id));
    }

    private void OnReceive(ENetPacketPeer peer, int channel)
    {
        if (!_ids.TryGetValue(peer.GetInstanceId(), out var id))
            return;

        _events.Enqueue(LinkEvent.Data(id, channel, peer.GetPacket()));
    }

    private void OnDrop(ENetPacketPeer peer, DisconnectReason reason)
    {
        if (!_ids.Remove(peer.GetInstanceId(), out var id))
            return;

        _peers.Remove(id.Value);
        _events.Enqueue(LinkEvent.Closed(id, reason));
    }

    internal static int FlagsFor(int channelId) =>
        NetChannels.IsReliable(channelId)
            ? (int)ENetPacketPeer.FlagReliable
            : (int)ENetPacketPeer.FlagUnsequenced;
}
