using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using Xunit.Abstractions;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class TickBudgetTests
{
    private readonly ITestOutputHelper _output;

    public TickBudgetTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TickBudgetReport_LimitMs_IsTwoCitedToSpec12()
    {
        Assert.Equal(2.0, TickBudgetReport.LimitMs);
        Assert.Equal(2.0, new SoakConfig().TickLimitMs);
        Assert.Equal(30u, new SoakConfig().WarmupTicks);
    }

    [Fact]
    public void TickLog_Close_DiscardsWarmupAndComputesMaxMean()
    {
        var log = new TickLog();
        for (uint i = 0; i < 30; i++)
            log.Add(new TickSample(i, 9.0));
        log.Add(new TickSample(30, 0.5));
        log.Add(new TickSample(31, 1.5));
        log.Add(new TickSample(32, 1.0));

        var report = log.Close(30);

        Assert.Equal(30u, report.WarmupTicks);
        Assert.Equal(3u, report.SampleCount);
        Assert.Equal(1.5, report.MaxCpuMs);
        Assert.Equal(1.0, report.MeanCpuMs);
        Assert.True(report.Pass);
    }

    [Fact]
    public void TickLog_Close_MaxAboveLimit_DoesNotPass()
    {
        var log = new TickLog();
        for (uint i = 0; i < 30; i++)
            log.Add(new TickSample(i, 0.1));
        log.Add(new TickSample(30, 2.01));

        Assert.False(log.Close(30).Pass);
    }

    [Fact]
    public void TickLog_Add_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TickLog().Add(null!));
    }

    [Fact]
    public void TickLog_Close_WithoutPostWarmupSamples_Throws()
    {
        var log = new TickLog();
        for (uint i = 0; i < 30; i++)
            log.Add(new TickSample(i, 0.1));

        Assert.Throws<InvalidOperationException>(() => log.Close(30));
    }

    [Fact]
    public void TickBudget_LoadedEightSeatU6Map_MaxCpuMsAtMostTwo()
    {
        var config = new SoakConfig
        {
            DurationTicks = SoakDuration.TicksForSimMinutes(1),
            WarmupTicks = 30,
            TickLimitMs = TickBudgetReport.LimitMs,
        };

        // First Intake batch is at BatchIntervalTicks (450). Thirty warmup ticks
        // never compile MailSpawner.EmitBatch, so a cold process max (~15 ms) is
        // method JIT, not TickOnce work. One two-batch pass completes that JIT.
        // LimitMs stays 2.0 (spec/12). Do not use chapter 07's 8 ms.
        SoakSession.Start(new SoakConfig
        {
            DurationTicks = (uint)MailSpawnConstants.BatchIntervalTicks * 2 + 30,
            WarmupTicks = 30,
        }).Run();

        var session = SoakSession.Start(config);
        var report = session.Run();
        var budget = report.TickBudget;

        _output.WriteLine(
            $"max={budget.MaxCpuMs:F4} mean={budget.MeanCpuMs:F4} samples={budget.SampleCount} limit={TickBudgetReport.LimitMs}");
        foreach (var sample in session.Ticks.Samples
            .Skip((int)budget.WarmupTicks)
            .OrderByDescending(s => s.CpuMs)
            .Take(8))
        {
            _output.WriteLine($"  tick={sample.Tick} cpuMs={sample.CpuMs:F4}");
        }

        Assert.Equal(SoakRoster.SeatCount, session.Roster.Seats.Count);
        Assert.Equal(SoakRoster.RealCount, session.Roster.Seats.Count(s => s.Kind == SeatKind.Real));
        Assert.Equal(SoakRoster.BotCount, session.Roster.Seats.Count(s => s.Kind == SeatKind.Bot));
        Assert.Equal(SoakRoster.SeatCount, report.ConnectedSeats);
        Assert.Equal(config.DurationTicks, report.TicksRun);
        Assert.Equal(30u, budget.WarmupTicks);
        Assert.Equal(config.DurationTicks - 30u, budget.SampleCount);
        Assert.Equal(2.0, TickBudgetReport.LimitMs);
        Assert.Equal(2.0, config.TickLimitMs);

        var atlas = session.Server.World.Atlas;
        Assert.NotNull(atlas);
        Assert.Equal("m0_test", atlas.Id);
        Assert.True(session.Server.World.MailSpawner!.SpawnedValue > 0);
        Assert.True(
            MailSpawnConstants.BatchIntervalTicks < config.DurationTicks,
            "Duration must include the first Intake batch so the sample is loaded.");
        Assert.Contains(
            session.Roster.Seats,
            s => s.Client.Prediction.Pose != PlayerPose.Origin);

        Assert.True(
            budget.MaxCpuMs <= TickBudgetReport.LimitMs,
            $"max {budget.MaxCpuMs} ms exceeded {TickBudgetReport.LimitMs} ms (mean {budget.MeanCpuMs} ms)");
        Assert.True(budget.Pass);
        Assert.True(report.Criterion5);
    }
}
