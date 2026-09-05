using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public static class SegmentInterest
{
    public const int DefaultRadiusMetres = 150;

    public const int DefaultTileCm = 200;

    public static int PeriodTicks => TickClock.TickHz / 2;

    public static bool Hits(
        int originXcm,
        int originYcm,
        IReadOnlyList<TileCoord> tiles,
        int tileCm,
        int radiusMetres)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);
        if (radiusMetres < 0)
            throw new ArgumentOutOfRangeException(nameof(radiusMetres), radiusMetres, null);
        if (tiles.Count == 0) return false;

        int radiusCm = radiusMetres * 100;
        long limit = (long)radiusCm * radiusCm;
        for (int i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            int minX = tile.X * tileCm;
            int minY = tile.Y * tileCm;
            int qx = Math.Clamp(originXcm, minX, minX + tileCm);
            int qy = Math.Clamp(originYcm, minY, minY + tileCm);
            long dx = originXcm - qx;
            long dy = originYcm - qy;
            if (dx * dx + dy * dy <= limit)
                return true;
        }

        return false;
    }
}
