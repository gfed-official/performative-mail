using System.Collections.Generic;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public sealed class MapPingBoard
{
    public const int LifetimeSeconds = MapPingLimits.LifetimeSeconds;
    public const int RateLimitSeconds = MapPingLimits.RateLimitSeconds;

    public static int LifetimeTicks => MapPingLimits.LifetimeTicks;

    public static int RateLimitTicks => MapPingLimits.RateLimitTicks;

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

    public void Observe(MapPing ping)
    {
        for (int i = 0; i < _pings.Count; i++)
        {
            if (_pings[i].Id == ping.Id)
                return;
        }

        _pings.Add(ping);
        if (ping.Id >= _nextId)
            _nextId = ping.Id + 1;
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
