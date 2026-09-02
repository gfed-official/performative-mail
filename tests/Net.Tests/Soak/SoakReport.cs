namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakReport
{
    public required uint TicksRun { get; init; }

    public required int ConnectedSeats { get; init; }

    public required IReadOnlyList<HashWitness> Mismatches { get; init; }

    public required IReadOnlyList<HashWitness> Witnesses { get; init; }

    public required TickBudgetReport TickBudget { get; init; }

    public required bool Criterion1 { get; init; }

    public required bool Criterion5 { get; init; }
}
