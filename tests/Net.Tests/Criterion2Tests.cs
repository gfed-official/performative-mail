using Xunit.Abstractions;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class Criterion2Tests
{
    private readonly ITestOutputHelper _output;

    public Criterion2Tests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void PredictionErrorSample_Rms_UsesHorizontalPair()
    {
        var samples = new[]
        {
            new PredictionErrorSample(10, 0, 0, 0),
            new PredictionErrorSample(0, 0, 0, 0),
        };

        Assert.Equal(Math.Sqrt(50), PredictionErrorSample.RmsCentimetres(samples), 10);
    }

    [Fact]
    public void Criterion2_200ms5pct_PredictedVsServerRmsBelow10cm()
    {
        var run = Criterion2Harness.Run(
            oneWayDelay: TimeSpan.FromMilliseconds(200),
            dropRate: 0.05,
            seed: 31,
            warmupTicks: TickClock.TickHz,
            walkTicks: TickClock.TickHz * 8);

        PrintRms(run.RmsCentimetres, run.SampleCount);
        Assert.True(run.GotPong, "Ping must complete on the conditioned path.");
        Assert.True(run.SampleCount >= TickClock.TickHz * 6, $"Need samples across the 8 s walk, got {run.SampleCount}.");
        Assert.True(run.RmsCentimetres < 10.0, $"RMS {run.RmsCentimetres:F3} cm is not below 10 cm.");
    }

    [Fact]
    public void Criterion2_100ms0pct_ReconcileDeltaAtMost1cm()
    {
        var run = Criterion2Harness.Run(
            oneWayDelay: TimeSpan.FromMilliseconds(100),
            dropRate: 0,
            seed: 31,
            warmupTicks: TickClock.TickHz,
            walkTicks: TickClock.TickHz * 8);

        PrintRms(run.RmsCentimetres, run.SampleCount);
        Assert.True(run.GotPong, "Ping must complete on the conditioned path.");
        Assert.True(
            run.MaxReconcileDeltaCm <= 1.0,
            $"Max reconcile delta {run.MaxReconcileDeltaCm:F3} cm is above 1 cm.");
    }

    private void PrintRms(double rms, int samples)
    {
        var line = $"Criterion2 RMS: {rms:F3} cm ({samples} samples)";
        Console.WriteLine(line);
        _output.WriteLine(line);
    }
}

internal sealed class Criterion2Harness
{
    private const int HeadingPeriodTicks = TickClock.TickHz * 2;
    private const int PingAttempts = 8;

    private readonly int _delayTicks;
    private readonly ConditionedTransport _serverLink;
    private readonly ConditionedTransport _clientLink;
    private readonly ServerRuntime _server;
    private readonly ClientRuntime _client;
    private readonly Dictionary<uint, PlayerPose> _predicted = new Dictionary<uint, PlayerPose>();
    private readonly List<PredictionErrorSample> _samples = new List<PredictionErrorSample>();

    private uint _lastSampledTick = uint.MaxValue;
    private double _maxReconcileDeltaCm;
    private int _walkTick;

    private Criterion2Harness(TimeSpan oneWayDelay, double dropRate, int seed)
    {
        _delayTicks = ConditionedTransport.TicksFor(oneWayDelay);
        var loopback = new LoopbackTransport();
        _serverLink = new ConditionedTransport(loopback.A, oneWayDelay, dropRate, seed);
        _clientLink = new ConditionedTransport(loopback.B, oneWayDelay, dropRate, seed ^ unchecked((int)0xA5A5A5A5));
        _server = new ServerRuntime(LoopbackLink.OverPipes(_serverLink));
        _client = new ClientRuntime();
        _server.Start();
        _client.Connect(_clientLink);
    }

    public static RunResult Run(
        TimeSpan oneWayDelay,
        double dropRate,
        int seed,
        int warmupTicks,
        int walkTicks)
    {
        var harness = new Criterion2Harness(oneWayDelay, dropRate, seed);
        harness.Join();
        harness.PingUntilPong();
        harness.SeedEstimate();
        harness.Walk(warmupTicks, sample: false);
        harness.Walk(walkTicks, sample: true);
        return harness.Finish();
    }

    private void Join()
    {
        Pump(_delayTicks * 2);
        Assert.True(_client.LocalPlayer.HasValue);
    }

    private void PingUntilPong()
    {
        for (uint attempt = 0; attempt < PingAttempts && !_client.LastPong.HasValue; attempt++)
        {
            _client.SendPing(attempt);
            Pump(_delayTicks * 2);
        }
    }

    private void SeedEstimate()
    {
        var delayTicks = (uint)_delayTicks;
        _client.SeedServerTickEstimate(_server.World.CurrentTick + delayTicks);
    }

    private void Walk(int ticks, bool sample)
    {
        for (int i = 0; i < ticks; i++)
        {
            var cmd = ScriptedWalk(_walkTick++);
            _client.SubmitInput(in cmd);
            var pending = _client.Prediction.Pending;
            var stamped = pending[pending.Count - 1];
            _predicted[stamped.Tick] = _client.Prediction.Pose;

            _client.SendInputs();
            Advance();
            _server.TickOnce();

            var before = _client.Prediction.Pose;
            var snapshots = _client.SnapshotCount;
            _client.Receive();
            if (_client.SnapshotCount > snapshots)
            {
                var delta = PredictionErrorSample.HorizontalDistanceCm(before, _client.Prediction.Pose);
                if (delta > _maxReconcileDeltaCm)
                    _maxReconcileDeltaCm = delta;
            }

            if (sample)
                TrySample();
        }
    }

    private void TrySample()
    {
        if (_client.LocalPlayer is not EntityId local)
            return;
        if (!_server.World.Players.TryGet(local, out var body))
            return;
        if (!body.HasAppliedInput)
            return;
        if (body.LastProcessedInputTick == _lastSampledTick)
            return;
        if (!_predicted.TryGetValue(body.LastProcessedInputTick, out var predicted))
            return;

        _samples.Add(new PredictionErrorSample(in predicted, body.Pose));
        _lastSampledTick = body.LastProcessedInputTick;
    }

    private void Pump(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Advance();
            _server.TickOnce();
            _client.Receive();
        }
    }

    private void Advance()
    {
        _serverLink.AdvanceTicks(1);
        _clientLink.AdvanceTicks(1);
    }

    private RunResult Finish() =>
        new(
            PredictionErrorSample.RmsCentimetres(_samples),
            _maxReconcileDeltaCm,
            _samples.Count,
            _client.LastPong.HasValue);

    private static InputCmd ScriptedWalk(int walkTick)
    {
        var quarter = walkTick / HeadingPeriodTicks;
        var yaw = (ushort)((quarter % 4) * 16384);
        return new InputCmd(0, 0, MovementStep.AxisFull, yaw, InputButtons.None);
    }

    internal readonly record struct RunResult(
        double RmsCentimetres,
        double MaxReconcileDeltaCm,
        int SampleCount,
        bool GotPong);
}
