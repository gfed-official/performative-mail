namespace PerformativeMail.Sim.Run;

public static class JoinGate
{
    public static bool Allows(RunPhase phase) =>
        phase is RunPhase.Lobby or RunPhase.Prep;
}
