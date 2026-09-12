using System;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public static class SmallPort
{
    public const string BuildingId = "small_port";
    public const string BlueprintId = "bp_motorboat";
    public const int FootprintW = 3;
    public const int FootprintH = 4;
    public const int DeepWaterTiles = 2;

    public static TileCoord[] Occupied(TileCoord origin, Facing facing)
    {
        Size(facing, out int w, out int h);
        var tiles = new TileCoord[w * h];
        int n = 0;
        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
                tiles[n++] = new TileCoord(origin.X + dx, origin.Y + dy);
        }

        return tiles;
    }

    public static TileCoord ParkingZone(TileCoord origin, Facing facing)
    {
        Size(facing, out int w, out int h);
        switch (facing)
        {
            case Facing.North:
                return new TileCoord(origin.X + w / 2, origin.Y + h - 2);
            case Facing.East:
                return new TileCoord(origin.X + w - 2, origin.Y + h / 2);
            case Facing.South:
                return new TileCoord(origin.X + w / 2, origin.Y + 1);
            case Facing.West:
                return new TileCoord(origin.X + 1, origin.Y + h / 2);
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }

    public static TileCoord[] DeepTiles(TileCoord origin, Facing facing)
    {
        var park = ParkingZone(origin, facing);
        return new[] { park, Step(park, facing) };
    }

    public static TileCoord LoadingFace(TileCoord origin, Facing facing)
    {
        Size(facing, out int w, out int h);
        switch (facing)
        {
            case Facing.North:
                return new TileCoord(origin.X + w / 2, origin.Y - 1);
            case Facing.East:
                return new TileCoord(origin.X - 1, origin.Y + h / 2);
            case Facing.South:
                return new TileCoord(origin.X + w / 2, origin.Y + h);
            case Facing.West:
                return new TileCoord(origin.X + w, origin.Y + h / 2);
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }

    public static bool HoldsParked(TileCoord origin, Facing facing, VehicleBody vehicle, int tileCm)
    {
        if (vehicle is null) throw new ArgumentNullException(nameof(vehicle));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);
        return vehicle.Kind == VehicleKind.Motorboat
            && vehicle.IsParked
            && vehicle.Tile(tileCm).Equals(ParkingZone(origin, facing));
    }

    public static PlaceReject? RejectFootprint(PlacementField field, TileCoord origin, Facing facing)
    {
        if (field is null) throw new ArgumentNullException(nameof(field));

        var deep = DeepTiles(origin, facing);
        var covered = Occupied(origin, facing);
        int deepHits = 0;
        for (int i = 0; i < covered.Length; i++)
        {
            var tile = covered[i];
            bool berth = tile.Equals(deep[0]) || tile.Equals(deep[1]);
            if (berth)
            {
                if (!field.IsDeepWater(tile))
                    return PlaceReject.Water;
                deepHits++;
                continue;
            }

            if (field.IsWater(tile))
                return PlaceReject.Water;
        }

        return deepHits == DeepWaterTiles ? null : PlaceReject.Water;
    }

    private static void Size(Facing facing, out int w, out int h)
    {
        w = FootprintW;
        h = FootprintH;
        if (facing == Facing.East || facing == Facing.West)
        {
            w = FootprintH;
            h = FootprintW;
        }
    }

    private static TileCoord Step(TileCoord tile, Facing facing)
    {
        switch (facing)
        {
            case Facing.North: return new TileCoord(tile.X, tile.Y + 1);
            case Facing.East: return new TileCoord(tile.X + 1, tile.Y);
            case Facing.South: return new TileCoord(tile.X, tile.Y - 1);
            case Facing.West: return new TileCoord(tile.X - 1, tile.Y);
            default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }
}

public sealed class SmallPortSite : IRouteConsole
{
    public SmallPortSite(EntityId id, TileCoord origin, Facing facing)
    {
        Id = id;
        Origin = origin;
        Facing = facing;
        Route = new VehicleRoute();
    }

    public EntityId Id { get; }

    public TileCoord Origin { get; }

    public Facing Facing { get; }

    public VehicleRoute Route { get; }

    public NpcDriver? Driver { get; private set; }

    public TileCoord ParkingZone => SmallPort.ParkingZone(Origin, Facing);

    public TileCoord LoadingFace => SmallPort.LoadingFace(Origin, Facing);

    public void BindDriver(NpcDriver driver)
    {
        Driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }
}
