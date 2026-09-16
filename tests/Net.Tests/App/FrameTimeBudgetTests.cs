using PerformativeMail.App;

namespace PerformativeMail.Net.Tests.App;

public sealed class FrameTimeBudgetTests
{
    [Fact]
    public void LimitMs_IsSixteenPointSeven()
    {
        Assert.Equal(16.7, FrameTimeBudget.LimitMs);
    }

    [Fact]
    public void Close_DiscardsWarmupAndComputesMeanAndP99()
    {
        var log = new FrameTimeLog();
        for (int i = 0; i < 30; i++)
            log.Add(99.0);
        log.Add(1.0);
        log.Add(2.0);
        log.Add(3.0);
        log.Add(4.0);
        log.Add(5.0);
        log.Add(6.0);
        log.Add(7.0);
        log.Add(8.0);
        log.Add(9.0);
        log.Add(10.0);

        var report = log.Close(30);

        Assert.Equal(30, report.WarmupFrames);
        Assert.Equal(10, report.SampleCount);
        Assert.Equal(5.5, report.MeanMs);
        Assert.Equal(10.0, report.P99Ms);
        Assert.True(report.Pass);
    }

    [Fact]
    public void Close_MeanAboveLimit_DoesNotPass()
    {
        var log = new FrameTimeLog();
        for (int i = 0; i < 30; i++)
            log.Add(1.0);
        log.Add(16.8);
        log.Add(16.8);

        var report = log.Close(30);

        Assert.Equal(16.8, report.MeanMs);
        Assert.Equal(16.8, report.P99Ms);
        Assert.False(report.Pass);
        Assert.False(16.8 <= FrameTimeBudget.LimitMs);
    }

    [Fact]
    public void Close_P99UsesCeilingIndex()
    {
        var log = new FrameTimeLog();
        log.Add(0.0);
        for (int i = 0; i < 100; i++)
            log.Add(1.0);
        log.Add(5.0);

        var report = log.Close(1);

        Assert.Equal(101, report.SampleCount);
        int index = (int)Math.Ceiling(0.99 * 101) - 1;
        Assert.Equal(99, index);
        Assert.Equal(1.0, report.P99Ms);
        Assert.Equal((100 * 1.0 + 5.0) / 101, report.MeanMs);
    }
}
