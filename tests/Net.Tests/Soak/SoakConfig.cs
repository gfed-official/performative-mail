namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakConfig
{
    public uint DurationTicks { get; init; } = SoakDuration.Criterion1Ticks;

    public uint WarmupTicks { get; init; } = 30;

    public double TickLimitMs { get; init; } = TickBudgetReport.LimitMs;
}
