using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public sealed class MapPingBoard
{
    public const int LifetimeSeconds = 10;
    public const int RateLimitSeconds = 1;

    public static int LifetimeTicks => TickClock.TicksFromSeconds(LifetimeSeconds);

    public static int RateLimitTicks => TickClock.TicksFromSeconds(RateLimitSeconds);

    private readonly List<MapPing> _pings = new();
    private int _nextId = 1;
    private uint? _lastPlaceTick;

    public IReadOnlyList<MapPing> Visible => _pings;

    public bool TryPlace(TileCoord tile, MapPingKind kind, uint now, out MapPing ping)
    {
        Expire(now);
        if (_lastPlaceTick is uint last && now - last < (uint)RateLimitTicks)
        {
            ping = default;
            return false;
        }

        ping = new MapPing(_nextId++, tile, kind, now);
        _pings.Add(ping);
        _lastPlaceTick = now;
        return true;
    }

    public void Expire(uint now)
    {
        for (int i = _pings.Count - 1; i >= 0; i--)
        {
            if (now - _pings[i].PlacedTick >= (uint)LifetimeTicks)
                _pings.RemoveAt(i);
        }
    }
}
