using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Tests.Core;

public sealed class TickPacerTests
{
    [Fact]
    public void Advance_OneTickDuration_YieldsOneTick()
    {
        var pacer = TickPacer.AtTickRate();
        pacer.Reset(TimeSpan.Zero);
        Assert.Equal(1, pacer.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / TickClock.TickHz)));
    }

    [Fact]
    public void Advance_HalfTick_YieldsZero()
    {
        var pacer = TickPacer.AtTickRate();
        pacer.Reset(TimeSpan.Zero);
        Assert.Equal(0, pacer.Advance(TimeSpan.FromMilliseconds(16)));
    }

    [Fact]
    public void Advance_LongStall_CapsCatchUp()
    {
        var pacer = TickPacer.AtTickRate();
        pacer.Reset(TimeSpan.Zero);
        Assert.Equal(TickPacer.MaxCatchUpTicks, pacer.Advance(TimeSpan.FromSeconds(2)));
    }
}
