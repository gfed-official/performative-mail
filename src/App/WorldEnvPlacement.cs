using System;
using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public enum StreetEdge : byte
{
    East = 0,
    West = 1,
    North = 2,
    South = 3,
}

public enum EnvPropKind : byte
{
    Crate = 0,
    Cart = 1,
}

public readonly record struct EnvInstancePose(float X, float Y, float Z, float YawRadians);

public readonly record struct EnvPropPose(EnvPropKind Kind, float X, float Y, float Z, float YawRadians);

public static class WorldEnvPlacement
{
    public const float ArtTileMeters = 2f;
    public const float StreetHeightMeters = 0.08f;
    public const float CurbHeightMeters = 0.15f;
    public const float CurbThicknessMeters = 0.18f;
    public const float GrassHeightMeters = 0.04f;
    public const float SpawnPadHeightMeters = 0.12f;
    public const float SpawnPadScale = 0.9f;

    public static EnvInstancePose[] StreetTiles(StreetRecord[] streets, float tileMeters)
    {
        var street = StreetSet(streets);
        var poses = new EnvInstancePose[street.Count];
        int i = 0;
        foreach (var tile in street)
        {
            var at = WorldTilePlacement.TileCenter(tile, tileMeters);
            bool east = street.Contains(new TileCoord(tile.X + 1, tile.Y));
            bool west = street.Contains(new TileCoord(tile.X - 1, tile.Y));
            bool north = street.Contains(new TileCoord(tile.X, tile.Y + 1));
            bool south = street.Contains(new TileCoord(tile.X, tile.Y - 1));
            poses[i++] = new EnvInstancePose(at.X, 0f, at.Z, StreetTileYaw(east, west, north, south));
        }

        return poses;
    }

    public static EnvInstancePose[] StreetCurbs(StreetRecord[] streets, float tileMeters)
    {
        var street = StreetSet(streets);
        var poses = new List<EnvInstancePose>(street.Count * 2);
        foreach (var tile in street)
        {
            var at = WorldTilePlacement.TileCenter(tile, tileMeters);
            if (!street.Contains(new TileCoord(tile.X + 1, tile.Y)))
                poses.Add(CurbPose(at, tileMeters, StreetEdge.East));
            if (!street.Contains(new TileCoord(tile.X - 1, tile.Y)))
                poses.Add(CurbPose(at, tileMeters, StreetEdge.West));
            if (!street.Contains(new TileCoord(tile.X, tile.Y + 1)))
                poses.Add(CurbPose(at, tileMeters, StreetEdge.North));
            if (!street.Contains(new TileCoord(tile.X, tile.Y - 1)))
                poses.Add(CurbPose(at, tileMeters, StreetEdge.South));
        }

        return poses.ToArray();
    }

    public static EnvInstancePose[] LotGrass(LotRecord[] lots, PostOfficeRecord po, StreetRecord[] streets, float tileMeters)
    {
        var street = StreetSet(streets);
        var seen = new HashSet<TileCoord>();
        var poses = new List<EnvInstancePose>();
        AddRect(lots, po, street, seen, poses, tileMeters);
        return poses.ToArray();
    }

    public static EnvPropPose[] PostalClutter(
        PostOfficeRecord po,
        StreetRecord[] streets,
        float tileMeters)
    {
        var origin = WorldTilePlacement.FootprintOrigin(po.Tile, po.SizeTiles, tileMeters);
        var intake = WorldTilePlacement.TileCenter(po.IntakeTile, tileMeters);
        var toward = WorldTilePlacement.TowardNearestStreet(origin.X, origin.Z, streets, tileMeters);
        var street = Normalize(toward.X, toward.Z);
        var outboard = Normalize(intake.X - origin.X, intake.Z - origin.Z);
        if (outboard.X == 0f && outboard.Z == 0f)
            outboard = street;
        var side = (X: -street.Z, Z: street.X);
        float face = MathF.Max(po.SizeTiles.X, po.SizeTiles.Y) * tileMeters * 0.5f;

        return
        [
            Place(EnvPropKind.Crate, intake, outboard, 1.15f, side, 0.7f, 0.15f),
            Place(EnvPropKind.Crate, intake, outboard, 1.15f, side, -0.7f, -0.35f),
            Place(EnvPropKind.Cart, intake, outboard, 2.1f, side, 0f, YawToward(-outboard.X, -outboard.Z)),
            Place(
                EnvPropKind.Crate,
                origin,
                street,
                face + 0.4f,
                side,
                2.2f,
                0.4f),
            Place(
                EnvPropKind.Cart,
                origin,
                street,
                face + 0.8f,
                side,
                -2.4f,
                YawToward(street.X, street.Z)),
        ];
    }

    public static EnvInstancePose CurbPose((float X, float Y, float Z) tileCenter, float tileMeters, StreetEdge edge)
    {
        float half = tileMeters * 0.5f;
        switch (edge)
        {
            case StreetEdge.East:
                return new EnvInstancePose(tileCenter.X + half, 0f, tileCenter.Z, MathF.PI * 0.5f);
            case StreetEdge.West:
                return new EnvInstancePose(tileCenter.X - half, 0f, tileCenter.Z, -MathF.PI * 0.5f);
            case StreetEdge.North:
                return new EnvInstancePose(tileCenter.X, 0f, tileCenter.Z - half, MathF.PI);
            case StreetEdge.South:
                return new EnvInstancePose(tileCenter.X, 0f, tileCenter.Z + half, 0f);
            default:
                throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
        }
    }

    public static float StreetTileYaw(bool east, bool west, bool north, bool south)
    {
        bool alongX = east || west;
        bool alongZ = north || south;
        return alongX && !alongZ ? MathF.PI * 0.5f : 0f;
    }

    public static float YawToward(float dx, float dz) => MathF.Atan2(dx, dz);

    private static void AddRect(
        LotRecord[] lots,
        PostOfficeRecord po,
        HashSet<TileCoord> street,
        HashSet<TileCoord> seen,
        List<EnvInstancePose> poses,
        float tileMeters)
    {
        for (int i = 0; i < lots.Length; i++)
            AddFootprint(lots[i].Footprint, street, seen, poses, tileMeters);
        AddFootprint(po.Footprint, street, seen, poses, tileMeters);
    }

    private static void AddFootprint(
        TileRect footprint,
        HashSet<TileCoord> street,
        HashSet<TileCoord> seen,
        List<EnvInstancePose> poses,
        float tileMeters)
    {
        foreach (var tile in footprint.Tiles())
        {
            if (street.Contains(tile) || !seen.Add(tile))
                continue;
            var at = WorldTilePlacement.TileCenter(tile, tileMeters);
            poses.Add(new EnvInstancePose(at.X, 0f, at.Z, 0f));
        }
    }

    private static EnvPropPose Place(
        EnvPropKind kind,
        (float X, float Y, float Z) origin,
        (float X, float Z) along,
        float alongM,
        (float X, float Z) side,
        float sideM,
        float yaw) =>
        new(
            kind,
            origin.X + along.X * alongM + side.X * sideM,
            0f,
            origin.Z + along.Z * alongM + side.Z * sideM,
            yaw);

    private static (float X, float Z) Normalize(float x, float z)
    {
        float len = MathF.Sqrt(x * x + z * z);
        return len < 1e-8f ? (0f, 0f) : (x / len, z / len);
    }

    private static HashSet<TileCoord> StreetSet(StreetRecord[] streets)
    {
        var set = new HashSet<TileCoord>();
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null)
                continue;
            for (int t = 0; t < tiles.Length; t++)
                set.Add(tiles[t]);
        }

        return set;
    }
}
