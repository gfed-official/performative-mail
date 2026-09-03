using System;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class RunStateTests
{
    private static readonly RunPhase[] Phases = Enum.GetValues<RunPhase>();

    [Theory]
    [InlineData(RunPhase.Lobby, RunPhase.Generating, 1)]
    [InlineData(RunPhase.Generating, RunPhase.Prep, 1)]
    [InlineData(RunPhase.Prep, RunPhase.Delivery, 1)]
    [InlineData(RunPhase.Delivery, RunPhase.Payday, 1)]
    [InlineData(RunPhase.Payday, RunPhase.Draft, 1)]
    [InlineData(RunPhase.Draft, RunPhase.Prep, 1)]
    [InlineData(RunPhase.Delivery, RunPhase.Raid, 2)]
    [InlineData(RunPhase.Raid, RunPhase.Payday, 2)]
    [InlineData(RunPhase.Payday, RunPhase.Draft, 2)]
    [InlineData(RunPhase.Draft, RunPhase.Victory, 5)]
    [InlineData(RunPhase.Victory, RunPhase.Results, 5)]
    [InlineData(RunPhase.RunOver, RunPhase.Results, 1)]
    [InlineData(RunPhase.Delivery, RunPhase.RunOver, 1)]
    [InlineData(RunPhase.Raid, RunPhase.RunOver, 2)]
    [InlineData(RunPhase.Payday, RunPhase.RunOver, 1)]
    [InlineData(RunPhase.Results, RunPhase.Lobby, 5)]
    public void TryTransition_LegalEdge_Accepts(RunPhase from, RunPhase to, byte shift)
    {
        var state = new RunState(from, shift, 40);
        Assert.True(state.TryTransition(to, out var next));
        Assert.Equal(to, next.Phase);
    }

    [Theory]
    [InlineData(RunPhase.Delivery, RunPhase.Draft, 1)]
    [InlineData(RunPhase.Delivery, RunPhase.Prep, 1)]
    [InlineData(RunPhase.Lobby, RunPhase.Prep, 1)]
    [InlineData(RunPhase.Prep, RunPhase.Payday, 1)]
    [InlineData(RunPhase.Draft, RunPhase.Delivery, 1)]
    [InlineData(RunPhase.Victory, RunPhase.Lobby, 5)]
    [InlineData(RunPhase.RunOver, RunPhase.Lobby, 1)]
    [InlineData(RunPhase.Delivery, RunPhase.Raid, 1)]
    [InlineData(RunPhase.Delivery, RunPhase.Payday, 2)]
    [InlineData(RunPhase.Draft, RunPhase.Victory, 4)]
    [InlineData(RunPhase.Draft, RunPhase.Prep, 5)]
    [InlineData(RunPhase.Lobby, RunPhase.Lobby, 1)]
    public void TryTransition_IllegalEdge_Rejects(RunPhase from, RunPhase to, byte shift)
    {
        var state = new RunState(from, shift, 40);
        Assert.False(state.TryTransition(to, out var next));
        Assert.Equal(state, next);
    }

    [Fact]
    public void TryTransition_EveryPair_MatchesChapter01Edges()
    {
        for (byte shift = 1; shift <= RunState.ShiftCount; shift++)
        {
            foreach (var from in Phases)
            {
                foreach (var to in Phases)
                {
                    var state = new RunState(from, shift, 0);
                    bool accepted = state.TryTransition(to, out _);
                    Assert.Equal(ExpectedLegal(from, to, shift), accepted);
                }
            }
        }
    }

    [Fact]
    public void TryTransition_DraftToPrep_IncrementsShift()
    {
        var state = new RunState(RunPhase.Draft, 1, 10);
        Assert.True(state.TryTransition(RunPhase.Prep, 99, out var next));
        Assert.Equal(2, next.Shift);
        Assert.Equal(99u, next.PhaseDeadlineTick);
    }

    [Fact]
    public void TryTransition_ResultsToLobby_ResetsShift()
    {
        var state = new RunState(RunPhase.Results, 5, 10);
        Assert.True(state.TryTransition(RunPhase.Lobby, out var next));
        Assert.Equal(RunPhase.Lobby, next.Phase);
        Assert.Equal(1, next.Shift);
    }

    [Fact]
    public void TryTransition_LegalEdge_KeepsDeadlineUnlessReplaced()
    {
        var state = new RunState(RunPhase.Lobby, 1, 40);
        Assert.True(state.TryTransition(RunPhase.Generating, out var kept));
        Assert.Equal(40u, kept.PhaseDeadlineTick);
        Assert.True(state.TryTransition(RunPhase.Generating, 80, out var replaced));
        Assert.Equal(80u, replaced.PhaseDeadlineTick);
    }

    [Fact]
    public void InLobby_StartsShiftOneAtTickZero()
    {
        var state = RunState.InLobby();
        Assert.Equal(RunPhase.Lobby, state.Phase);
        Assert.Equal(1, state.Shift);
        Assert.Equal(0u, state.PhaseDeadlineTick);
    }

    [Fact]
    public void Constructor_ShiftOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunState(RunPhase.Lobby, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RunState(RunPhase.Lobby, 6, 0));
    }

    [Fact]
    public void Walk_Shift1ToVictory_FollowsHappyPath()
    {
        var state = RunState.InLobby();
        Assert.True(state.TryTransition(RunPhase.Generating, out state));
        Assert.True(state.TryTransition(RunPhase.Prep, out state));
        Assert.True(state.TryTransition(RunPhase.Delivery, out state));
        Assert.True(state.TryTransition(RunPhase.Payday, out state));
        Assert.True(state.TryTransition(RunPhase.Draft, out state));
        Assert.True(state.TryTransition(RunPhase.Prep, out state));
        Assert.Equal(2, state.Shift);

        for (byte shift = 2; shift <= 4; shift++)
        {
            Assert.True(state.TryTransition(RunPhase.Delivery, out state));
            Assert.True(state.TryTransition(RunPhase.Raid, out state));
            Assert.True(state.TryTransition(RunPhase.Payday, out state));
            Assert.True(state.TryTransition(RunPhase.Draft, out state));
            Assert.True(state.TryTransition(RunPhase.Prep, out state));
            Assert.Equal(shift + 1, state.Shift);
        }

        Assert.True(state.TryTransition(RunPhase.Delivery, out state));
        Assert.True(state.TryTransition(RunPhase.Raid, out state));
        Assert.True(state.TryTransition(RunPhase.Payday, out state));
        Assert.True(state.TryTransition(RunPhase.Draft, out state));
        Assert.True(state.TryTransition(RunPhase.Victory, out state));
        Assert.True(state.TryTransition(RunPhase.Results, out state));
        Assert.True(state.TryTransition(RunPhase.Lobby, out state));
        Assert.Equal(RunPhase.Lobby, state.Phase);
        Assert.Equal(1, state.Shift);
    }

    private static bool ExpectedLegal(RunPhase from, RunPhase to, byte shift)
    {
        return (from, to) switch
        {
            (RunPhase.Lobby, RunPhase.Generating) => true,
            (RunPhase.Generating, RunPhase.Prep) => true,
            (RunPhase.Prep, RunPhase.Delivery) => true,
            (RunPhase.Delivery, RunPhase.Raid) => shift >= 2,
            (RunPhase.Delivery, RunPhase.Payday) => shift == 1,
            (RunPhase.Delivery, RunPhase.RunOver) => true,
            (RunPhase.Raid, RunPhase.Payday) => true,
            (RunPhase.Raid, RunPhase.RunOver) => true,
            (RunPhase.Payday, RunPhase.Draft) => true,
            (RunPhase.Payday, RunPhase.RunOver) => true,
            (RunPhase.Draft, RunPhase.Prep) => shift < 5,
            (RunPhase.Draft, RunPhase.Victory) => shift == 5,
            (RunPhase.RunOver, RunPhase.Results) => true,
            (RunPhase.Victory, RunPhase.Results) => true,
            (RunPhase.Results, RunPhase.Lobby) => true,
            _ => false,
        };
    }
}
