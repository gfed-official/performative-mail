using System;

namespace PerformativeMail.Sim.Run;

public static class RunTransitions
{
    public static bool IsLegal(RunPhase from, RunPhase to, byte shift)
    {
        if (shift < 1 || shift > RunState.ShiftCount)
            return false;

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
            (RunPhase.Draft, RunPhase.Prep) => shift < RunState.ShiftCount,
            (RunPhase.Draft, RunPhase.Victory) => shift == RunState.ShiftCount,
            (RunPhase.RunOver, RunPhase.Results) => true,
            (RunPhase.Victory, RunPhase.Results) => true,
            (RunPhase.Results, RunPhase.Lobby) => true,
            _ => false,
        };
    }

    public static bool TryPrimaryNext(RunPhase from, byte shift, out RunPhase to)
    {
        to = from switch
        {
            RunPhase.Lobby => RunPhase.Generating,
            RunPhase.Generating => RunPhase.Prep,
            RunPhase.Prep => RunPhase.Delivery,
            RunPhase.Delivery => shift == 1 ? RunPhase.Payday : RunPhase.Raid,
            RunPhase.Raid => RunPhase.Payday,
            RunPhase.Payday => RunPhase.Draft,
            RunPhase.Draft => shift < RunState.ShiftCount ? RunPhase.Prep : RunPhase.Victory,
            RunPhase.RunOver => RunPhase.Results,
            RunPhase.Victory => RunPhase.Results,
            RunPhase.Results => RunPhase.Lobby,
            _ => throw new ArgumentOutOfRangeException(nameof(from), from, null),
        };
        return IsLegal(from, to, shift);
    }
}
