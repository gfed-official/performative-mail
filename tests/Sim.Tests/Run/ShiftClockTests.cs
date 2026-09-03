using System;
using System.IO;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class ShiftClockTests
{
    private static readonly BalanceTable Balance = BalanceCatalog.LoadFile(
        Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath));

    [Fact]
    public void TicksFromSeconds_PrepSixty_IsEighteenHundredAtThirtyHz()
    {
        Assert.Equal(30, TickClock.TickHz);
        Assert.Equal(1800, TickClock.TicksFromSeconds(60));
        Assert.Equal(1800, ShiftDurations.Ticks(RunPhase.Prep, 1, Balance));
    }

    [Fact]
    public void TicksFromSeconds_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TickClock.TicksFromSeconds(-1));
    }

    [Fact]
    public void EnterPrep_Shift1_DeadlineIsEighteenHundred()
    {
        var clock = EnterPrep();
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
        Assert.Equal(1800u, clock.State.PhaseDeadlineTick);
        Assert.Equal(1800u, clock.State.RemainingTicks(0));
    }

    [Fact]
    public void AdvanceTo_PrepDeadline_EntersDelivery()
    {
        var clock = EnterPrep();
        clock.AdvanceTo(1799);
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
        Assert.Equal(1u, clock.State.RemainingTicks(clock.Now));

        clock.AdvanceTo(1800);
        Assert.Equal(RunPhase.Delivery, clock.State.Phase);
        Assert.Equal(1800u, clock.Now);
        Assert.Equal((uint)TickClock.TicksFromSeconds(240), clock.State.RemainingTicks(clock.Now));
    }

    [Fact]
    public void SetReady_AllConnected_EndsPrepBeforeDeadline()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        clock.Connect(2);
        Assert.False(clock.SetReady(1, true));
        Assert.Equal(RunPhase.Prep, clock.State.Phase);

        Assert.True(clock.SetReady(2, true));
        Assert.Equal(RunPhase.Delivery, clock.State.Phase);
        Assert.Equal(0u, clock.Now);
        Assert.True(clock.State.PhaseDeadlineTick > 0);
    }

    [Fact]
    public void SetReady_UnknownPlayer_DoesNotEndPrep()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        Assert.False(clock.SetReady(2, true));
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
    }

    [Fact]
    public void Disconnect_LastUnready_EndsPrep()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        clock.Connect(2);
        clock.SetReady(1, true);
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
        clock.Disconnect(2);
        Assert.Equal(RunPhase.Delivery, clock.State.Phase);
    }

    [Fact]
    public void SetReady_EmptyLobby_DoesNotEndPrep()
    {
        var clock = EnterPrep();
        Assert.False(clock.SetReady(1, true));
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
    }

    [Fact]
    public void TrySetPaused_Solo_FreezesNow()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        Assert.True(clock.TrySetPaused(true));
        clock.AdvanceTo(1800);
        Assert.Equal(0u, clock.Now);
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
        Assert.True(clock.Paused);
    }

    [Fact]
    public void TrySetPaused_TwoPlayers_Rejected()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        clock.Connect(2);
        Assert.False(clock.TrySetPaused(true));
        Assert.False(clock.Paused);
        clock.AdvanceTo(10);
        Assert.Equal(10u, clock.Now);
    }

    [Fact]
    public void Connect_SecondPlayer_Unpauses()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        Assert.True(clock.TrySetPaused(true));
        clock.Connect(2);
        Assert.False(clock.Paused);
        clock.AdvanceTo(5);
        Assert.Equal(5u, clock.Now);
    }

    [Fact]
    public void ReadyDuringPause_StillEndsPrep()
    {
        var clock = EnterPrep();
        clock.Connect(1);
        Assert.True(clock.TrySetPaused(true));
        Assert.True(clock.SetReady(1, true));
        Assert.Equal(RunPhase.Delivery, clock.State.Phase);
        Assert.Equal(0u, clock.Now);
    }

    [Fact]
    public void TryEnter_IllegalEdge_Rejected()
    {
        var clock = EnterPrep();
        var before = clock.State;
        Assert.False(clock.TryEnter(RunPhase.Payday));
        Assert.Equal(before, clock.State);
    }

    [Fact]
    public void TryAllPicked_DraftShift1_EntersNextPrep()
    {
        var clock = EnterPrep();
        Assert.True(clock.TryEnter(RunPhase.Delivery));
        Assert.True(clock.TryEnter(RunPhase.Payday));
        Assert.True(clock.TryEnter(RunPhase.Draft));
        Assert.True(clock.TryAllPicked());
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
        Assert.Equal(2, clock.State.Shift);
    }

    [Fact]
    public void TryAllPicked_OutsideDraft_False()
    {
        var clock = EnterPrep();
        Assert.False(clock.TryAllPicked());
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
    }

    [Fact]
    public void AdvanceTo_DraftDeadline_EntersNextPrep()
    {
        var clock = EnterPrep();
        Assert.True(clock.TryEnter(RunPhase.Delivery));
        Assert.True(clock.TryEnter(RunPhase.Payday));
        Assert.True(clock.TryEnter(RunPhase.Draft));
        uint deadline = clock.State.PhaseDeadlineTick;
        Assert.Equal((uint)TickClock.TicksFromSeconds(30), deadline);
        clock.AdvanceTo(deadline);
        Assert.Equal(RunPhase.Prep, clock.State.Phase);
        Assert.Equal(2, clock.State.Shift);
    }

    [Fact]
    public void AdvanceTo_DeliveryShift2_EntersRaidAtNinetySecondsLeft()
    {
        var clock = EnterPrep();
        Assert.True(clock.TryEnter(RunPhase.Delivery));
        Assert.True(clock.TryEnter(RunPhase.Payday));
        Assert.True(clock.TryEnter(RunPhase.Draft));
        Assert.True(clock.TryEnter(RunPhase.Prep));
        Assert.True(clock.TryEnter(RunPhase.Delivery));
        Assert.Equal(2, clock.State.Shift);

        uint deliveryEnd = clock.State.PhaseDeadlineTick;
        uint raidTicks = (uint)TickClock.TicksFromSeconds(ShiftDurations.RaidWindowSeconds);
        clock.AdvanceTo(deliveryEnd - raidTicks - 1);
        Assert.Equal(RunPhase.Delivery, clock.State.Phase);

        clock.AdvanceTo(deliveryEnd - raidTicks);
        Assert.Equal(RunPhase.Raid, clock.State.Phase);
        Assert.Equal(deliveryEnd, clock.State.PhaseDeadlineTick);

        clock.AdvanceTo(deliveryEnd);
        Assert.Equal(RunPhase.Payday, clock.State.Phase);
    }

    [Fact]
    public void ShouldReplicate_OncePerSecondOrPhaseOrPause()
    {
        Assert.False(ShiftClock.ShouldReplicate(29, RunPhase.Prep, false, 0, RunPhase.Prep, false));
        Assert.True(ShiftClock.ShouldReplicate(30, RunPhase.Prep, false, 0, RunPhase.Prep, false));
        Assert.True(ShiftClock.ShouldReplicate(0, RunPhase.Delivery, false, 0, RunPhase.Prep, false));
        Assert.True(ShiftClock.ShouldReplicate(0, RunPhase.Prep, true, 0, RunPhase.Prep, false));
    }

    [Fact]
    public void AdvanceTo_Backward_Throws()
    {
        var clock = EnterPrep();
        clock.AdvanceTo(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTo(9));
    }

    private static ShiftClock EnterPrep()
    {
        var clock = new ShiftClock(Balance, RunState.InLobby());
        Assert.True(clock.TryEnter(RunPhase.Generating));
        Assert.True(clock.TryEnter(RunPhase.Prep));
        return clock;
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, BalanceCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/balance.json");
    }
}
