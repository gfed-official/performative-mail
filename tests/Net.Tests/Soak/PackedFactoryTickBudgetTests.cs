using System.Diagnostics;
using System.Runtime;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Net;
using Xunit.Abstractions;

namespace PerformativeMail.Net.Tests.Soak;

[Collection(SoakCollection.Name)]
public sealed class PackedFactoryTickBudgetTests
{
    public const double LimitMs = 8.0;

    private const uint WarmupTicks = 30;
    private const uint DurationTicks = 330;
    private const uint PrimeTicks = 30;

    private readonly ITestOutputHelper _output;

    public PackedFactoryTickBudgetTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PackedFactory_5000Letters_ServerTickP99AtMostEight()
    {
        var world = new SimWorld();
        int items = PackedFactory.Fill(world);
        Assert.Equal(PackedFactory.ItemCount, items);
        var packed = Assert.Single(world.Belts.Segments);
        Assert.Equal(PackedFactory.ItemCount / 2, packed.Lane(0).Count);
        Assert.Equal(PackedFactory.ItemCount / 2, packed.Lane(1).Count);

        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), world);
        var log = new TickLog();
        var watch = new Stopwatch();
        var thread = Thread.CurrentThread;
        var priority = thread.Priority;
        thread.Priority = ThreadPriority.Highest;
        try
        {
            Pump(server, PrimeTicks, watch, log, recordCpu: false);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Pump(server, DurationTicks, watch, log, recordCpu: true);
        }
        finally
        {
            thread.Priority = priority;
        }

        var budget = log.Close(WarmupTicks);
        var afterSegment = Assert.Single(server.World.Belts.Segments);
        int after = afterSegment.Lane(0).Count + afterSegment.Lane(1).Count;

        _output.WriteLine(
            $"p99={budget.P99CpuMs:F4} max={budget.MaxCpuMs:F4} mean={budget.MeanCpuMs:F4} items={after} samples={budget.SampleCount} limit={LimitMs}");

        Assert.Equal(PackedFactory.ItemCount, after);
        Assert.Equal(PackedFactory.ItemCount / 2, afterSegment.Lane(0).Count);
        Assert.Equal(PackedFactory.ItemCount / 2, afterSegment.Lane(1).Count);
        Assert.Equal(DurationTicks - WarmupTicks, budget.SampleCount);
        Assert.True(
            budget.P99CpuMs <= LimitMs,
            $"p99 {budget.P99CpuMs} ms exceeded {LimitMs} ms (max {budget.MaxCpuMs} ms)");
    }

    private static void Pump(
        ServerRuntime server,
        uint ticks,
        Stopwatch watch,
        TickLog log,
        bool recordCpu)
    {
        for (uint i = 0; i < ticks; i++)
        {
            if (!recordCpu)
            {
                server.TickOnce();
                continue;
            }

            log.Add(new TickSample(server.World.CurrentTick, TimeTickOnce(watch, server)));
        }
    }

    private static double TimeTickOnce(Stopwatch watch, ServerRuntime server)
    {
        if (!TryBeginNoGc())
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            TryBeginNoGc();
        }

        try
        {
            watch.Restart();
            server.TickOnce();
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            EndNoGc();
        }
    }

    private static bool TryBeginNoGc()
    {
        try
        {
            return GC.TryStartNoGCRegion(16 * 1024 * 1024);
        }
        catch (OutOfMemoryException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void EndNoGc()
    {
        try
        {
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
                GC.EndNoGCRegion();
        }
        catch (InvalidOperationException)
        {
        }
    }
}
