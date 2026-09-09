using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class ConstructPlacement
{
    public const float BeltHeightMeters = 0.16f;
    public const float ChestHeightMeters = 0.72f;
    public const float WallHeightMeters = 1.55f;
    public const float SorterHeightMeters = 1.15f;
    public const float DefaultHeightMeters = 0.8f;
    public const float LaneItemHeightMeters = 0.12f;
    public const float LaneItemLiftMeters = 0.28f;
    public const float LaneOffsetMeters = 0.35f;

    public static (float X, float Y, float Z) Origin(
        TileCoord tile,
        byte footprintW,
        byte footprintH,
        Facing rotation,
        float tileMeters)
    {
        var size = VisualTiles(footprintW, footprintH, rotation);
        return WorldTilePlacement.FootprintOrigin(tile, size, tileMeters);
    }

    public static TileCoord VisualTiles(byte footprintW, byte footprintH, Facing rotation)
    {
        int w = footprintW < 1 ? 1 : footprintW;
        int h = footprintH < 1 ? 1 : footprintH;
        if (w == 1 && h == 1)
            return new TileCoord(1, 1);
        if ((rotation == Facing.East || rotation == Facing.West) && w != h)
            return new TileCoord(h, w);
        return new TileCoord(w, h);
    }

    public static (float X, float Z) Toward(Facing facing)
    {
        switch (facing)
        {
            case Facing.North:
                return (0f, -1f);
            case Facing.East:
                return (1f, 0f);
            case Facing.South:
                return (0f, 1f);
            case Facing.West:
                return (-1f, 0f);
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }

    public static (float X, float Y, float Z) BoxSize(
        BuildingBehaviour behaviour,
        byte footprintW,
        byte footprintH,
        Facing rotation,
        float tileMeters)
    {
        var tiles = VisualTiles(footprintW, footprintH, rotation);
        float across = tiles.X * tileMeters;
        float along = tiles.Y * tileMeters;
        switch (behaviour)
        {
            case BuildingBehaviour.Belt:
                return (tileMeters * 0.9f, BeltHeightMeters, tileMeters * 0.92f);
            case BuildingBehaviour.Container:
                return (0.9f, ChestHeightMeters, 0.9f);
            case BuildingBehaviour.Wall:
                return (tileMeters * 0.92f, WallHeightMeters, 0.22f);
            case BuildingBehaviour.Sorter:
                return (across * 0.9f, SorterHeightMeters, along * 0.9f);
            case BuildingBehaviour.Splitter:
            case BuildingBehaviour.Merger:
                return (tileMeters * 0.85f, 0.7f, tileMeters * 0.85f);
            case BuildingBehaviour.Inserter:
                return (0.45f, 0.9f, 0.7f);
            case BuildingBehaviour.Pipe:
                return (0.35f, 0.35f, tileMeters * 0.92f);
            case BuildingBehaviour.Gate:
                return (tileMeters * 0.92f, WallHeightMeters, 0.28f);
            case BuildingBehaviour.Spike:
                return (0.7f, 0.55f, 0.7f);
            case BuildingBehaviour.Turret:
                return (0.8f, 1.2f, 0.8f);
            case BuildingBehaviour.Alarm:
                return (0.4f, 1.1f, 0.4f);
            case BuildingBehaviour.VehicleDepot:
                return (across * 0.9f, 1.4f, along * 0.9f);
            case BuildingBehaviour.Port:
            case BuildingBehaviour.Pier:
                return (across * 0.9f, 0.45f, along * 0.9f);
            case BuildingBehaviour.Pump:
                return (0.8f, 1.0f, 0.8f);
            default:
                throw new ArgumentOutOfRangeException(nameof(behaviour), behaviour, null);
        }
    }

    public static (float X, float Y, float Z) LaneItem(
        IReadOnlyList<TileCoord> tiles,
        Facing facing,
        int positionCm,
        byte lane,
        int tileCm)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (tiles.Count == 0) throw new ArgumentException("Lane path needs at least one tile.", nameof(tiles));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);

        float tileM = tileCm / 100f;
        float along = positionCm / 100f;
        int index = (int)MathF.Floor(along / tileM);
        if (index < 0) index = 0;
        if (index >= tiles.Count) index = tiles.Count - 1;
        float local = along - index * tileM;
        var center = WorldTilePlacement.TileCenter(tiles[index], tileM);
        var toward = Toward(facing);
        float mid = local - tileM * 0.5f;
        float perpX = -toward.Z;
        float perpZ = toward.X;
        float laneOff = lane == 0 ? -LaneOffsetMeters : LaneOffsetMeters;
        return (
            center.X + toward.X * mid + perpX * laneOff,
            LaneItemLiftMeters,
            center.Z + toward.Z * mid + perpZ * laneOff);
    }
}
