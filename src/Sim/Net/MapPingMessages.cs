using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Net;

public static class MapPingLimits
{
    public const int LifetimeSeconds = 10;

    public const int RateLimitSeconds = 1;

    public const byte MaxKind = 4;

    public static int LifetimeTicks => TickClock.TicksFromSeconds(LifetimeSeconds);

    public static int RateLimitTicks => TickClock.TicksFromSeconds(RateLimitSeconds);

    public static bool IsKind(byte kind) => kind <= MaxKind;
}

public readonly record struct MapPingRequest(int TileX, int TileY, byte Kind);

public readonly record struct MapPingEvent(int Id, int TileX, int TileY, byte Kind, uint PlacedTick);
