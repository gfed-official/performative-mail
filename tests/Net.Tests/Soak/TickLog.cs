namespace PerformativeMail.Net.Tests.Soak;

public sealed class TickLog
{
    private readonly List<TickSample> _samples = new();

    public IReadOnlyList<TickSample> Samples => _samples;

    public void Add(TickSample sample)
    {
        if (sample is null)
            throw new ArgumentNullException(nameof(sample));

        _samples.Add(sample);
    }

    public TickBudgetReport Close(uint warmupTicks)
    {
        if (_samples.Count <= warmupTicks)
        {
            throw new InvalidOperationException(
                $"TickLog needs more than {warmupTicks} samples to discard warmup; has {_samples.Count}.");
        }

        double max = double.NegativeInfinity;
        double sum = 0;
        uint count = 0;
        var ranked = new double[_samples.Count - (int)warmupTicks];
        for (int i = (int)warmupTicks; i < _samples.Count; i++)
        {
            var cpu = _samples[i].CpuMs;
            ranked[count] = cpu;
            if (cpu > max)
                max = cpu;
            sum += cpu;
            count++;
        }

        Array.Sort(ranked);
        int p99At = (int)Math.Ceiling(0.99 * count) - 1;

        return new TickBudgetReport
        {
            WarmupTicks = warmupTicks,
            SampleCount = count,
            MaxCpuMs = max,
            MeanCpuMs = sum / count,
            P99CpuMs = ranked[p99At],
        };
    }
}
