namespace PerformativeMail.Sim.Core;

public static class TickClock
{
    public const int TickHz = 30;

    public static double TickDurationSeconds => 1.0 / TickHz;
}
