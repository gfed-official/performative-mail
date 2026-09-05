using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public readonly record struct GroundSlab(float X, float Y, float Z, float SizeX, float SizeY, float SizeZ);

public static class WorldTilePlacement
{
    public const float GroundThicknessM = 0.04f;

    public static GroundSlab SmallIslandGround() =>
        GroundForTiles(WorldGen.SmallIslandTiles, WorldGen.SmallIslandTiles, WorldGen.SmallIslandTileCm / 100f);

    public static GroundSlab GroundForTiles(int widthTiles, int heightTiles, float tileMeters)
    {
        if (widthTiles <= 0) throw new ArgumentOutOfRangeException(nameof(widthTiles));
        if (heightTiles <= 0) throw new ArgumentOutOfRangeException(nameof(heightTiles));
        if (tileMeters <= 0f) throw new ArgumentOutOfRangeException(nameof(tileMeters));

        var a = ViewFrame.From(new PlayerPose(0, 0, 0, 0));
        var b = ViewFrame.From(new PlayerPose(
            (int)(widthTiles * tileMeters * 100f),
            (int)(heightTiles * tileMeters * 100f),
            0,
            0));
        float minX = Math.Min(a.X, b.X);
        float maxX = Math.Max(a.X, b.X);
        float minZ = Math.Min(a.Z, b.Z);
        float maxZ = Math.Max(a.Z, b.Z);
        return new GroundSlab(
            (minX + maxX) * 0.5f,
            -GroundThicknessM * 0.5f,
            (minZ + maxZ) * 0.5f,
            maxX - minX,
            GroundThicknessM,
            maxZ - minZ);
    }

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
