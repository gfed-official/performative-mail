using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public static class VehicleDepot
{
    public const string BuildingId = "vehicle_depot";
    public const int FootprintTiles = 3;

    public static TileCoord[] Occupied(TileCoord origin)
    {
        var tiles = new TileCoord[FootprintTiles * FootprintTiles];
        int n = 0;
        for (int dy = 0; dy < FootprintTiles; dy++)
        {
            for (int dx = 0; dx < FootprintTiles; dx++)
                tiles[n++] = new TileCoord(origin.X + dx, origin.Y + dy);
        }

        return tiles;
    }

    public static TileCoord ParkingZone(TileCoord origin) =>
        new TileCoord(origin.X + 1, origin.Y + 1);

    public static TileCoord LoadingFace(TileCoord origin, Facing facing) =>
        Step(ParkingZone(origin), facing);

    public static bool HoldsParked(TileCoord origin, VehicleBody vehicle, int tileCm)
    {
        if (vehicle is null) throw new ArgumentNullException(nameof(vehicle));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);
        return vehicle.IsParked && vehicle.Tile(tileCm).Equals(ParkingZone(origin));
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

public sealed class VehicleDepotSite
{
    public VehicleDepotSite(EntityId id, TileCoord origin, Facing facing)
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

    public TileCoord ParkingZone => VehicleDepot.ParkingZone(Origin);

    public TileCoord LoadingFace => VehicleDepot.LoadingFace(Origin, Facing);
}
