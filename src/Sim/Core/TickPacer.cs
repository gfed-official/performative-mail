using System;

namespace PerformativeMail.Sim.Core;

public struct TickPacer
{
    public const int MaxCatchUpTicks = 5;

    private TimeSpan _last;
    private bool _started;

    public static TickPacer AtTickRate() => default;

    public void Reset(TimeSpan wallNow)
    {
        _last = wallNow;
        _started = true;
    }

    public int Advance(TimeSpan wallNow)
    {
        if (!_started)
        {
            Reset(wallNow);
            return 0;
        }

        var elapsed = wallNow - _last;
        if (elapsed <= TimeSpan.Zero)
            return 0;

        long ticksPerSim = TimeSpan.TicksPerSecond / TickClock.TickHz;
        int due = (int)(elapsed.Ticks / ticksPerSim);
        if (due <= 0)
            return 0;

        if (due > MaxCatchUpTicks)
        {
            _last = wallNow;
            return MaxCatchUpTicks;
        }

        _last += TimeSpan.FromTicks(due * ticksPerSim);
        return due;
    }
}
