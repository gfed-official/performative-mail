using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class WorldTilePlacement
{
    public static (float X, float Y, float Z) TileCenter(TileCoord tile, float tileMeters)
    {
        var view = ViewFrame.From(new PlayerPose(
            (int)((tile.X + 0.5f) * tileMeters * 100f),
            (int)((tile.Y + 0.5f) * tileMeters * 100f),
            0,
            0));
        return (view.X, 0f, view.Z);
    }

    public static (float X, float Y, float Z) FootprintOrigin(TileCoord tile, TileCoord sizeTiles, float tileMeters)
    {
        float cx = tile.X + sizeTiles.X * 0.5f;
        float cy = tile.Y + sizeTiles.Y * 0.5f;
        var view = ViewFrame.From(new PlayerPose(
            (int)(cx * tileMeters * 100f),
            (int)(cy * tileMeters * 100f),
            0,
            0));
        return (view.X, 0f, view.Z);
    }

    public static (float X, float Z) TowardNearestStreet(
        float originX,
        float originZ,
        StreetRecord[] streets,
        float tileMeters)
    {
        float best = float.MaxValue;
        float dx = 0f;
        float dz = 0f;
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null)
                continue;
            for (int t = 0; t < tiles.Length; t++)
            {
                var at = TileCenter(tiles[t], tileMeters);
                float ex = at.X - originX;
                float ez = at.Z - originZ;
                float d = ex * ex + ez * ez;
                if (d >= best)
                    continue;
                best = d;
                dx = ex;
                dz = ez;
            }
        }

        return best == float.MaxValue ? (0f, 0f) : (dx, dz);
    }
}
