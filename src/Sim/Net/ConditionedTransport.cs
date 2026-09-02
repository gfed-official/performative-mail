using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Net;

public sealed class ConditionedTransport : ITransport
{
    public const int UnreliableChannel = 0;

    private readonly ITransport _inner;
    private readonly int _delayTicks;
    private readonly double _dropRate;
    private readonly Random _drops;
    private readonly Queue<HeldPacket> _held = new Queue<HeldPacket>();
    private int _tick;

    public ConditionedTransport(ITransport inner, TimeSpan oneWayDelay, double dropRate, int seed)
        : this(inner, TicksFor(oneWayDelay), dropRate, seed)
    {
    }

    public ConditionedTransport(ITransport inner, int delayTicks, double dropRate, int seed)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (delayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(delayTicks), "Delay ticks must be non-negative.");
        if (dropRate < 0.0 || dropRate > 1.0)
            throw new ArgumentOutOfRangeException(nameof(dropRate), "Drop rate must be in [0, 1].");

        _delayTicks = delayTicks;
        _dropRate = dropRate;
        _drops = new Random(seed);
    }

    public int Tick => _tick;

    public int DelayTicks => _delayTicks;

    public static int TicksFor(TimeSpan oneWayDelay)
    {
        if (oneWayDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(oneWayDelay), "One-way delay must be non-negative.");

        return (int)Math.Round(oneWayDelay.TotalSeconds * TickClock.TickHz, MidpointRounding.AwayFromZero);
    }

    public void AdvanceTicks(int ticks = 1)
    {
        if (ticks < 0)
            throw new ArgumentOutOfRangeException(nameof(ticks), "Advance ticks must be non-negative.");

        _tick += ticks;
        FlushDue();
    }

    public void Advance(TimeSpan delta) => AdvanceTicks(TicksFor(delta));

    public void Send(int channelId, byte[] payload)
    {
        if (payload is null)
            throw new ArgumentNullException(nameof(payload));

        if (channelId == UnreliableChannel && _dropRate > 0.0 && _drops.NextDouble() < _dropRate)
            return;

        _held.Enqueue(new HeldPacket(_tick + _delayTicks, channelId, (byte[])payload.Clone()));
        FlushDue();
    }

    public bool Poll(out int channelId, out byte[] payload) => _inner.Poll(out channelId, out payload);

    private void FlushDue()
    {
        while (_held.Count > 0 && _held.Peek().DeliverAtTick <= _tick)
        {
            var packet = _held.Dequeue();
            _inner.Send(packet.ChannelId, packet.Payload);
        }
    }

    private readonly struct HeldPacket
    {
        public HeldPacket(int deliverAtTick, int channelId, byte[] payload)
        {
            DeliverAtTick = deliverAtTick;
            ChannelId = channelId;
            Payload = payload;
        }

        public int DeliverAtTick { get; }

        public int ChannelId { get; }

        public byte[] Payload { get; }
    }
}
