namespace PerformativeMail.Net.Tests.Soak;

public sealed class FactoryDesyncReport
{
    public const int SegmentCount = 30;

    public const double MaxResendsPerSegmentPerMinute = 1.0;

    public required int Segments { get; init; }

    public required uint TicksRun { get; init; }

    public required int ChecksumResends { get; init; }

    public required double ResendsPerSegmentPerMinute { get; init; }

    public required int EarlyEndpointRenders { get; init; }

    public required bool Pass { get; init; }
}
