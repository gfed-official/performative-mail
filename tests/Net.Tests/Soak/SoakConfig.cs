namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakConfig
{
    public uint DurationTicks { get; init; } = SoakDuration.Criterion1Ticks;

    public uint WarmupTicks { get; init; } = 30;

    /// <summary>
    /// Unmeasured TickOnce pumps before Stopwatch samples. Excluded from
    /// TickLog. WarmupTicks stays the counted discard inside TickLog.Close.
    /// </summary>
    public uint PrimeTicks { get; init; }

    public double TickLimitMs { get; init; } = TickBudgetReport.LimitMs;
}
