using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class LoopbackTransportTests
{
    [Fact]
    public void Send_ThenPoll_RoundTripsPayloadAndChannel()
    {
        var loopback = new LoopbackTransport();
        var payload = new byte[] { 1, 2, 3 };

        loopback.A.Send(7, payload);

        Assert.True(loopback.B.Poll(out var channelId, out var received));
        Assert.Equal(7, channelId);
        Assert.Equal(payload, received);
    }

    [Fact]
    public void Send_CopiesPayload_CallerMutationDoesNotChangeQueuedPacket()
    {
        var loopback = new LoopbackTransport();
        var payload = new byte[] { 1, 2, 3 };

        loopback.A.Send(0, payload);
        payload[0] = 9;

        Assert.True(loopback.B.Poll(out _, out var received));
        Assert.Equal(new byte[] { 1, 2, 3 }, received);
    }
}
