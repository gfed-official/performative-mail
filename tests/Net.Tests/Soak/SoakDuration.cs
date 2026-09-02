using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Net.Tests.Soak;

public static class SoakDuration
{
    public const uint Criterion1Ticks = 18_000;

    /// <summary>
    /// Unmeasured same-session ticks covering four Intake batch windows
    /// (interval plus max jitter each). Two throwaway sessions still left
    /// 4–8 ms GC/JIT spikes in the measured window. Not a second WarmupTicks.
    /// </summary>
    public const int JitPrimeBatchWindows = 4;

    public static uint JitPrimeTicks { get; } =
        (uint)(JitPrimeBatchWindows * (MailSpawnConstants.BatchIntervalTicks
            + MailSpawnConstants.BatchJitterSeconds * TickClock.TickHz));

    public static uint TicksForSimMinutes(int minutes)
    {
        if (minutes < 0)
            throw new ArgumentOutOfRangeException(nameof(minutes));

        return (uint)(minutes * 60 * TickClock.TickHz);
    }
}
