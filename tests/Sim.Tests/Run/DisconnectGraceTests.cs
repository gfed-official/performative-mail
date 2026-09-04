using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class DisconnectGraceTests
{
    private static readonly EntityId One = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly EntityId Two = EntityId.FromClassAndCounter(EntityClass.Player, 2);

    [Fact]
    public void HoldTicks_OneHundredTwentySecondsAtThirtyHz_IsThirtySixHundred()
    {
        Assert.Equal(30, TickClock.TickHz);
        Assert.Equal(3600, TickClock.TicksFromSeconds(120));
        Assert.Equal(3600, DisconnectGrace.HoldTicks);
        Assert.Equal(1800, DisconnectGrace.EmptyTicks);
    }

    [Fact]
    public void AdvanceTo_BeforeDropTick_KeepsSeat()
    {
        var grace = new DisconnectGrace();
        Assert.True(grace.Hold(7, One, 10, connectedAfter: 1));

        grace.AdvanceTo(10 + (uint)DisconnectGrace.HoldTicks - 1);
        Assert.True(grace.IsHeld(One));
        Assert.Empty(grace.TakeExpired());
        Assert.False(grace.EndedWithoutResults);
    }

    [Fact]
    public void AdvanceTo_AtDropTick_ExpiresSeat()
    {
        var grace = new DisconnectGrace();
        Assert.True(grace.Hold(7, One, 10, connectedAfter: 1));

        grace.AdvanceTo(10 + (uint)DisconnectGrace.HoldTicks);
        var expired = grace.TakeExpired();
        Assert.Equal(One, Assert.Single(expired));
        Assert.False(grace.IsHeld(One));
        Assert.False(grace.TryResume(7, out _));
    }

    [Fact]
    public void TryResume_WithinHold_ReturnsSameEntity()
    {
        var grace = new DisconnectGrace();
        grace.Hold(7, One, 0, connectedAfter: 1);

        Assert.True(grace.TryResume(7, out var player));
        Assert.Equal(One, player);
        Assert.False(grace.IsHeld(One));
        Assert.False(grace.TryResume(7, out _));
    }

    [Fact]
    public void TryResume_AccountZero_Fails()
    {
        var grace = new DisconnectGrace();
        grace.Hold(0, One, 0, connectedAfter: 1);

        Assert.False(grace.TryResume(0, out _));
        Assert.True(grace.IsHeld(One));
    }

    [Fact]
    public void Hold_LastSeat_EndsWithoutResultsAtSixtySeconds()
    {
        var grace = new DisconnectGrace();
        grace.Hold(7, One, 40, connectedAfter: 0);

        grace.AdvanceTo(40 + (uint)DisconnectGrace.EmptyTicks - 1);
        Assert.False(grace.EndedWithoutResults);
        Assert.True(grace.IsHeld(One));

        grace.AdvanceTo(40 + (uint)DisconnectGrace.EmptyTicks);
        Assert.True(grace.EndedWithoutResults);
        Assert.True(grace.IsHeld(One));
    }

    [Fact]
    public void TryResume_CancelsEmptyTimer()
    {
        var grace = new DisconnectGrace();
        grace.Hold(7, One, 0, connectedAfter: 0);
        Assert.True(grace.TryResume(7, out _));

        grace.AdvanceTo((uint)DisconnectGrace.EmptyTicks);
        Assert.False(grace.EndedWithoutResults);
    }

    [Fact]
    public void Hold_WithOtherConnected_DoesNotStartEmpty()
    {
        var grace = new DisconnectGrace();
        grace.Hold(7, One, 0, connectedAfter: 1);
        grace.Hold(8, Two, 0, connectedAfter: 1);

        grace.AdvanceTo((uint)DisconnectGrace.EmptyTicks);
        Assert.False(grace.EndedWithoutResults);
        Assert.Equal(2, grace.HeldCount);
    }
}
