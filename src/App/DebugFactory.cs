using PerformativeMail.Sim;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class DebugFactory
{
    public static readonly TileCoord WallTile = new(7, 1);
    public static readonly TileCoord ChestTile = new(7, 3);
    public static readonly TileCoord BeltStart = new(8, 3);
    public static readonly TileCoord SorterTile = new(12, 2);
    public const int BeltTiles = 4;
    public const Facing Travel = Facing.East;

    public static int Seed(SimWorld world)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (world.Constructs is not ConstructRegistry constructs)
            return 0;

        int placed = 0;
        placed += Place(constructs, "wall_wood", WallTile);
        placed += Place(constructs, "chest", ChestTile);
        for (int i = 0; i < BeltTiles; i++)
            placed += Place(constructs, BeltNetwork.BuildingId, new TileCoord(BeltStart.X + i, BeltStart.Y));
        placed += Place(constructs, "address_sorter_mk1", SorterTile);

        world.Belts.Compile(constructs.All);
        if (world.Belts.Segments.Count > 0)
            world.Belts.Segments[0].TryInsert(0, 1, 0.25f);
        return placed;
    }

    private static int Place(ConstructRegistry constructs, string buildingId, TileCoord tile)
    {
        int hp = constructs.TryGetBuilding(buildingId, out var building) ? building.Hp : 1;
        var id = EntityId.FromClassAndCounter(EntityClass.Construct, (uint)(constructs.Count + 1));
        var row = new ConstructRecord(id, buildingId, tile, Travel, default, hp, hp);
        return constructs.TryApplyPlaced(row) ? 1 : 0;
    }
}
