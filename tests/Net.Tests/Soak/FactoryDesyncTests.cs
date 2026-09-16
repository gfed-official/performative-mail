using Xunit.Abstractions;

namespace PerformativeMail.Net.Tests.Soak;

[Collection(SoakCollection.Name)]
public sealed class FactoryDesyncTests
{
    private readonly ITestOutputHelper _output;

    public FactoryDesyncTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void FactoryDesync_ThirtySegments_TenSimMinutes_UnderLossBudget()
    {
        var report = FactoryDesyncSession.Run();
        var line =
            $"U10.3 segments={report.Segments} ticks={report.TicksRun} resends={report.ChecksumResends} resendsPerSegmentPerMinute={report.ResendsPerSegmentPerMinute:F3} earlyEndpointRenders={report.EarlyEndpointRenders} pass={report.Pass}";
        Console.WriteLine(line);
        _output.WriteLine(line);

        Assert.Equal(FactoryDesyncReport.SegmentCount, report.Segments);
        Assert.Equal(SoakDuration.TicksForSimMinutes(10), report.TicksRun);
        Assert.Equal(0, report.EarlyEndpointRenders);
        Assert.True(
            report.ResendsPerSegmentPerMinute <= FactoryDesyncReport.MaxResendsPerSegmentPerMinute,
            $"resends per segment per minute {report.ResendsPerSegmentPerMinute:F3} exceeds {FactoryDesyncReport.MaxResendsPerSegmentPerMinute}");
        Assert.True(report.Pass);
    }
}
