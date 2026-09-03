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
}
