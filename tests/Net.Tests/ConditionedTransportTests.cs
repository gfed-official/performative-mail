using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class ConditionedTransportTests
{
    [Fact]
    public void TicksFor_200ms_IsSixAt30Hz()
    {
        Assert.Equal(6, ConditionedTransport.TicksFor(TimeSpan.FromMilliseconds(200)));
        Assert.Equal(3, ConditionedTransport.TicksFor(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(TickClock.TickHz, 30);
    }

    [Fact]
    public void Send_ZeroDelay_CopiesPayloadToPeer()
    {
        var loopback = new LoopbackTransport();
        var send = new ConditionedTransport(loopback.A, TimeSpan.Zero, 0, seed: 1);
        var payload = new byte[] { 9, 8, 7 };

        send.Send(0, payload);
        payload[0] = 1;

        Assert.True(loopback.B.Poll(out var channelId, out var received));
        Assert.Equal(0, channelId);
        Assert.Equal(new byte[] { 9, 8, 7 }, received);
    }

    [Fact]
    public void Send_200msDelay_ReleasesAfterAdvance()
    {
        var loopback = new LoopbackTransport();
        var delay = TimeSpan.FromMilliseconds(200);
        var send = new ConditionedTransport(loopback.A, delay, 0, seed: 1);
        var payload = new byte[] { 4, 5, 6 };

        send.Send(0, payload);
        Assert.False(loopback.B.Poll(out _, out _));

        send.AdvanceTicks(5);
        Assert.False(loopback.B.Poll(out _, out _));

        send.AdvanceTicks(1);
        Assert.True(loopback.B.Poll(out var channelId, out var received));
        Assert.Equal(0, channelId);
        Assert.Equal(payload, received);
    }

    [Fact]
    public void Send_Channel0_DropsAtRateOne()
    {
        var loopback = new LoopbackTransport();
        var send = new ConditionedTransport(loopback.A, TimeSpan.Zero, dropRate: 1, seed: 1);

        send.Send(ConditionedTransport.UnreliableChannel, new byte[] { 1 });
        Assert.False(loopback.B.Poll(out _, out _));

        send.Send(2, new byte[] { 2, 3 });
        Assert.True(loopback.B.Poll(out var channelId, out var received));
        Assert.Equal(2, channelId);
        Assert.Equal(new byte[] { 2, 3 }, received);
    }

    [Fact]
    public void Send_SameSeed_RepeatsDropPattern()
    {
        var first = DropMask(seed: 7, packets: 32);
        var second = DropMask(seed: 7, packets: 32);
        var other = DropMask(seed: 8, packets: 32);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Contains(true, first);
        Assert.Contains(false, first);
    }

    private static bool[] DropMask(int seed, int packets)
    {
        var loopback = new LoopbackTransport();
        var send = new ConditionedTransport(loopback.A, TimeSpan.Zero, dropRate: 0.5, seed);
        var mask = new bool[packets];
        for (int i = 0; i < packets; i++)
        {
            send.Send(0, new[] { (byte)i });
            mask[i] = !loopback.B.Poll(out _, out _);
        }

        return mask;
    }
}
