using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Net.Tests.Soak;

public static class SoakDuration
{
    public const uint Criterion1Ticks = 18_000;

    /// <summary>
    /// Unmeasured ticks covering two Intake batches (interval plus max jitter
    /// each) so spawn, inventory flush, and bot take JIT before Stopwatch
    /// samples. Not a second WarmupTicks discard.
    /// </summary>
    public static uint JitPrimeTicks { get; } =
        (uint)(2 * (MailSpawnConstants.BatchIntervalTicks
            + MailSpawnConstants.BatchJitterSeconds * TickClock.TickHz));

    public static uint TicksForSimMinutes(int minutes)
    {
        if (minutes < 0)
            throw new ArgumentOutOfRangeException(nameof(minutes));

        return (uint)(minutes * 60 * TickClock.TickHz);
    }
}
