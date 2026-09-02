using System;
using System.Collections.Concurrent;

namespace PerformativeMail.Sim.Net;

public sealed class LoopbackTransport
{
    public ITransport A { get; }

    public ITransport B { get; }

    public LoopbackTransport()
    {
        var aToB = new ConcurrentQueue<(int ChannelId, byte[] Payload)>();
        var bToA = new ConcurrentQueue<(int ChannelId, byte[] Payload)>();
        A = new Endpoint(aToB, bToA);
        B = new Endpoint(bToA, aToB);
    }

    private sealed class Endpoint : ITransport
    {
        private readonly ConcurrentQueue<(int ChannelId, byte[] Payload)> _outgoing;
        private readonly ConcurrentQueue<(int ChannelId, byte[] Payload)> _incoming;

        public Endpoint(
            ConcurrentQueue<(int ChannelId, byte[] Payload)> outgoing,
            ConcurrentQueue<(int ChannelId, byte[] Payload)> incoming)
        {
            _outgoing = outgoing;
            _incoming = incoming;
        }

        public void Send(int channelId, byte[] payload)
        {
            _outgoing.Enqueue((channelId, (byte[])payload.Clone()));
        }

        public bool Poll(out int channelId, out byte[] payload)
        {
            if (_incoming.TryDequeue(out var packet))
            {
                channelId = packet.ChannelId;
                payload = packet.Payload;
                return true;
            }

            channelId = 0;
            payload = Array.Empty<byte>();
            return false;
        }
    }
}
