namespace PerformativeMail.Net.Tests.Soak;

public sealed class TickSample
{
    public TickSample(uint tick, double cpuMs)
    {
        Tick = tick;
        CpuMs = cpuMs;
    }

    public uint Tick { get; }

    public double CpuMs { get; }
}
