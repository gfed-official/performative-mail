using System;

namespace PerformativeMail.Sim.Core;

public static class TickClock
{
    public const int TickHz = 30;

    public static double TickDurationSeconds => 1.0 / TickHz;

    public static int TicksFromSeconds(int seconds)
    {
        if (seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, null);

        return seconds * TickHz;
    }
}
