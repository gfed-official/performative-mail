using PerformativeMail.Sim.Net;

namespace PerformativeMail.App;

public sealed class RenderClock
{
    private uint _tick;
    private TimeSpan _wall;
    private bool _anchored;

    public void Reset()
    {
        _anchored = false;
        _tick = 0;
        _wall = TimeSpan.Zero;
    }

    public void Anchor(uint serverTick, TimeSpan wallNow)
    {
        if (_anchored && serverTick == _tick)
            return;

        _tick = serverTick;
        _wall = wallNow;
        _anchored = true;
    }

    public bool TryNow(TimeSpan wallNow, out TimeSpan serverTime)
    {
        if (!_anchored)
        {
            serverTime = default;
            return false;
        }

        serverTime = InterpolationBuffer.TimeOfTick(_tick) + (wallNow - _wall);
        return true;
    }
}
