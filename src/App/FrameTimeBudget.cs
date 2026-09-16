namespace PerformativeMail.App;

public static class FrameTimeBudget
{
    public const double LimitMs = 16.7;
}

public readonly record struct FrameTimeReport(
    int WarmupFrames,
    int SampleCount,
    double MeanMs,
    double P99Ms,
    bool Pass);

public sealed class FrameTimeLog
{
    private readonly List<double> _samples = new();

    public int Count => _samples.Count;

    public void Add(double ms) => _samples.Add(ms);

    public FrameTimeReport Close(int warmup)
    {
        if (warmup < 0)
            throw new ArgumentOutOfRangeException(nameof(warmup), warmup, null);
        if (_samples.Count <= warmup)
        {
            throw new InvalidOperationException(
                $"FrameTimeLog needs more than {warmup} samples to discard warmup; has {_samples.Count}.");
        }

        int n = _samples.Count - warmup;
        var slice = new double[n];
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double ms = _samples[warmup + i];
            slice[i] = ms;
            sum += ms;
        }

        Array.Sort(slice);
        int index = (int)Math.Ceiling(0.99 * n) - 1;
        if (index < 0)
            index = 0;
        if (index >= n)
            index = n - 1;

        double mean = sum / n;
        return new FrameTimeReport(warmup, n, mean, slice[index], mean <= FrameTimeBudget.LimitMs);
    }
}
