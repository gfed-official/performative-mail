using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class HashTraceTests
{
    [Fact]
    public void Criterion1Ticks_IsTenSimMinutesAtTickHz()
    {
        Assert.Equal(30, TickClock.TickHz);
        Assert.Equal(18_000u, SoakDuration.TicksForSimMinutes(10));
        Assert.Equal(18_000u, SoakDuration.Criterion1Ticks);
    }

    [Fact]
    public void Record_AppendsWitnesses()
    {
        var trace = new HashTrace();
        var first = Witness(1, 11, 1, 0xA, (new ConnectionId(0), 0xA));
        var second = Witness(2, 11, 2, 0xB, (new ConnectionId(0), 0xB));
        trace.Record(first);
        trace.Record(second);

        Assert.Equal(2, trace.Witnesses.Count);
        Assert.Same(first, trace.Witnesses[0]);
        Assert.Same(second, trace.Witnesses[1]);
    }

    [Fact]
    public void Check_MatchingViewerHashes_IsMatch()
    {
        var witness = Witness(
            4,
            3,
            2,
            0xFEED,
            (new ConnectionId(0), 0xFEED),
            (new ConnectionId(1), 0xFEED));

        var verdict = new HashTrace().Check(witness);

        Assert.Equal(HashVerdict.Match.Instance, verdict);
    }

    [Fact]
    public void Check_EmptyViewers_IsMatch()
    {
        var witness = Witness(1, 1, 0, 0x1);
        Assert.Equal(HashVerdict.Match.Instance, new HashTrace().Check(witness));
    }

    [Fact]
    public void Check_ViewerHashDiffers_IsHashMismatch()
    {
        var seat = new ConnectionId(3);
        var witness = Witness(
            9,
            8,
            4,
            0x10,
            (new ConnectionId(1), 0x10),
            (seat, 0x11));

        var verdict = new HashTrace().Check(witness);

        var mismatch = Assert.IsType<HashVerdict.HashMismatch>(verdict);
        Assert.Equal(seat, mismatch.Seat);
        Assert.Equal(0x10uL, mismatch.Expected);
        Assert.Equal(0x11uL, mismatch.Actual);
    }

    [Fact]
    public void Check_NullWitness_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new HashTrace().Check(null!));
        Assert.Throws<ArgumentNullException>(() => new HashTrace().Record(null!));
    }

    private static HashWitness Witness(
        uint tick,
        uint container,
        uint version,
        ulong serverHash,
        params (ConnectionId Seat, ulong Hash)[] viewers)
        => new(
            tick,
            new ContainerId(container),
            new ContainerVersion(version),
            serverHash,
            viewers);
}
