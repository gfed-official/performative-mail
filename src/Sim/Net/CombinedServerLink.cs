using System;

namespace PerformativeMail.Sim.Net;

public sealed class CombinedServerLink : IServerLink
{
    private readonly IServerLink _local;
    private readonly IServerLink _remote;
    private bool _preferRemote;

    public CombinedServerLink(IServerLink local, IServerLink remote)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
        _remote = remote ?? throw new ArgumentNullException(nameof(remote));
    }

    public bool TryPoll(out LinkEvent linkEvent)
    {
        if (!_preferRemote && _local.TryPoll(out linkEvent))
        {
            _preferRemote = true;
            return true;
        }

        if (_remote.TryPoll(out linkEvent))
        {
            _preferRemote = false;
            return true;
        }

        if (_local.TryPoll(out linkEvent))
        {
            _preferRemote = true;
            return true;
        }

        linkEvent = default;
        return false;
    }

    public void Send(ConnectionId to, int channelId, byte[] payload)
    {
        if (to.IsHostSeat)
            _local.Send(to, channelId, payload);
        else
            _remote.Send(to, channelId, payload);
    }

    public void Close(ConnectionId connection, DisconnectReason reason)
    {
        if (connection.IsHostSeat)
            _local.Close(connection, reason);
        else
            _remote.Close(connection, reason);
    }

    public void Dispose()
    {
        _local.Dispose();
        _remote.Dispose();
    }
}
