namespace PerformativeMail.Net.Tests.Soak;

public sealed class TickLog
{
    private readonly List<TickSample> _samples = new();

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
        for (int i = (int)warmupTicks; i < _samples.Count; i++)
        {
            var cpu = _samples[i].CpuMs;
            if (cpu > max)
                max = cpu;
            sum += cpu;
            count++;
        }

        return new TickBudgetReport
        {
            WarmupTicks = warmupTicks,
            SampleCount = count,
            MaxCpuMs = max,
            MeanCpuMs = sum / count,
        };
    }
}
