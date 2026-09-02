using PerformativeMail.Sim.Core;

namespace PerformativeMail.Net.Tests.Soak;

public static class SoakDuration
{
    public const uint Criterion1Ticks = 18_000;

    public static uint TicksForSimMinutes(int minutes)
    {
        if (minutes < 0)
            throw new ArgumentOutOfRangeException(nameof(minutes));

        return (uint)(minutes * 60 * TickClock.TickHz);
    }
}
