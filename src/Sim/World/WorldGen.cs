using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldGen
{
    public const int SmallIslandTiles = 300;
    public const int SmallIslandTileCm = 200;

    public static WorldTables GenerateSmallIsland(uint seed)
    {
        var heightStream = RngStream.Derive(seed, "heightmap");
        var result = HeightmapStage.Generate(heightStream, SmallIslandTiles, SmallIslandTiles, SmallIslandTileCm);
        var towns = RngStream.Derive(seed, "towns");
        var addresses = RngStream.Derive(seed, "addresses");
        var names = StreetCatalog.Load();
        var settlement = SettlementStage.Generate(
            towns,
            addresses,
            result.Heights,
            result.Buildable,
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm,
            names);
        return new WorldTables(
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm,
            result.Heights,
            settlement.Houses,
            result.Buildable,
            result.Valid,
            result.Attempts,
            settlement.PostOffice,
            settlement.Streets,
            settlement.Lots);
    }
}
