using System;

namespace PerformativeMail.Sim.Net;

public enum LinkEventKind : byte
{
    Opened = 1,
    Data = 2,
    Closed = 3,
}

public enum DisconnectReason : byte
{
    PeerLeft = 1,
    Timeout = 2,
    Rejected = 3,
    ServerShutdown = 4,
}

public readonly struct LinkEvent
{
    private LinkEvent(
        LinkEventKind kind,
        ConnectionId connection,
        int channelId,
        byte[] payload,
        DisconnectReason reason)
    {
        Kind = kind;
        Connection = connection;
        ChannelId = channelId;
        Payload = payload;
        Reason = reason;
    }

    public LinkEventKind Kind { get; }

    public ConnectionId Connection { get; }

    public int ChannelId { get; }

    public byte[] Payload { get; }

    public DisconnectReason Reason { get; }

    public static LinkEvent Opened(ConnectionId connection) =>
        new(LinkEventKind.Opened, connection, 0, Array.Empty<byte>(), default);

    public static LinkEvent Data(ConnectionId connection, int channelId, byte[] payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        return new LinkEvent(LinkEventKind.Data, connection, channelId, payload, default);
    }

    public static LinkEvent Closed(ConnectionId connection, DisconnectReason reason) =>
        new(LinkEventKind.Closed, connection, 0, Array.Empty<byte>(), reason);
}

public interface IServerLink : IDisposable
{
    bool TryPoll(out LinkEvent linkEvent);

    void Send(ConnectionId to, int channelId, byte[] payload);

    void Close(ConnectionId connection, DisconnectReason reason);
}
