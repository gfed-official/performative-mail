namespace PerformativeMail.Net.Tests.Soak;

/// <summary>
/// M0 criterion 5 in spec/12-milestones.md: server tick ≤ 2 ms with 8 players
/// on the test map. Not the 8 ms chapter 07 §8 / M5 loaded target.
/// </summary>
public sealed class TickBudgetReport
{
    public const double LimitMs = 2.0;

    public required uint WarmupTicks { get; init; }

    public required uint SampleCount { get; init; }

    public required double MaxCpuMs { get; init; }

    public required double MeanCpuMs { get; init; }

    public bool Pass => MaxCpuMs <= LimitMs;
}
