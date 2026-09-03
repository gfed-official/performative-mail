using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Net;

public sealed class LoopbackLink : IServerLink
{
    private readonly List<Seat> _seats = new List<Seat>();
    private readonly Queue<LinkEvent> _events = new Queue<LinkEvent>();
    private uint _nextId;
    private int _pollCursor;

    private LoopbackLink(uint firstId)
    {
        _nextId = firstId;
    }

    public static LoopbackLink OverPipes(params ITransport[] pipes) =>
        OverPipes((IReadOnlyList<ITransport>)pipes);

    public static LoopbackLink OverPipes(IReadOnlyList<ITransport> pipes, uint firstId = 0)
    {
        if (pipes is null)
            throw new ArgumentNullException(nameof(pipes));
        if (pipes.Count < 1)
            throw new ArgumentOutOfRangeException(nameof(pipes), "LoopbackLink needs at least one pipe.");

        var link = new LoopbackLink(firstId);
        for (int i = 0; i < pipes.Count; i++)
            link.Accept(pipes[i]);
        return link;
    }

    public ConnectionId Accept(ITransport pipe)
    {
        if (pipe is null)
            throw new ArgumentNullException(nameof(pipe));

        var id = new ConnectionId(_nextId++);
        _seats.Add(new Seat(id, pipe, open: true));
        _events.Enqueue(LinkEvent.Opened(id));
        return id;
    }

    public bool TryPoll(out LinkEvent linkEvent)
    {
        if (_events.Count > 0)
        {
            linkEvent = _events.Dequeue();
            return true;
        }

        int count = _seats.Count;
        for (int n = 0; n < count; n++)
        {
            int i = (_pollCursor + n) % count;
            var seat = _seats[i];
            if (!seat.Open)
                continue;
            if (!seat.Transport.Poll(out var channelId, out var payload))
                continue;

            _pollCursor = (i + 1) % count;
            linkEvent = LinkEvent.Data(seat.Id, channelId, payload);
            return true;
        }

        linkEvent = default;
        return false;
    }

    public void Send(ConnectionId to, int channelId, byte[] payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        for (int i = 0; i < _seats.Count; i++)
        {
            var seat = _seats[i];
            if (seat.Id != to || !seat.Open)
                continue;

            seat.Transport.Send(channelId, payload);
            return;
        }
    }

    public int OpenCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].Open)
                    n++;
            }

            return n;
        }
    }

    public bool IsOpen(ConnectionId connection)
    {
        for (int i = 0; i < _seats.Count; i++)
        {
            var seat = _seats[i];
            if (seat.Id == connection)
                return seat.Open;
        }

        return false;
    }

    public void Close(ConnectionId connection, DisconnectReason reason)
    {
        for (int i = 0; i < _seats.Count; i++)
        {
            var seat = _seats[i];
            if (seat.Id != connection || !seat.Open)
                continue;

            _seats[i] = new Seat(seat.Id, seat.Transport, open: false);
            _events.Enqueue(LinkEvent.Closed(connection, reason));
            return;
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _seats.Count; i++)
        {
            var seat = _seats[i];
            if (seat.Open)
                Close(seat.Id, DisconnectReason.ServerShutdown);
        }
    }

    private readonly struct Seat
    {
        public Seat(ConnectionId id, ITransport transport, bool open)
        {
            Id = id;
            Transport = transport;
            Open = open;
        }

        public ConnectionId Id { get; }

        public ITransport Transport { get; }

        public bool Open { get; }
    }
}
