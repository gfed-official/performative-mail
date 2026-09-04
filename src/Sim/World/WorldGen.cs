using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldGen
{
    public const int SmallIslandTiles = 300;
    public const int SmallIslandTileCm = 200;

    public static WorldTables GenerateSmallIsland(uint seed) =>
        GenerateSmallIsland(seed, validationAttempts: 1);

    public static WorldTables GenerateValidatedSmallIsland(uint seed, out int rerolls, int maxRerolls = 8)
    {
        if (maxRerolls < 0) throw new ArgumentOutOfRangeException(nameof(maxRerolls));

        rerolls = 0;
        var tables = GenerateSmallIsland(seed, 1);
        while (!tables.Valid && rerolls < maxRerolls)
        {
            rerolls++;
            tables = GenerateSmallIsland(unchecked(seed + (uint)rerolls), 1 + rerolls);
        }

        return tables;
    }

    private static WorldTables GenerateSmallIsland(uint seed, int validationAttempts)
    {
        var heightStream = RngStream.Derive(seed, "heightmap");
        var result = HeightmapStage.Generate(heightStream, SmallIslandTiles, SmallIslandTiles, SmallIslandTileCm);
        var towns = RngStream.Derive(seed, "towns");
        var addresses = RngStream.Derive(seed, "addresses");
        var roads = RngStream.Derive(seed, "roads");
        var resources = RngStream.Derive(seed, "resources");
        var spawns = RngStream.Derive(seed, "spawns");
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
        var connectivity = ConnectivityStage.Generate(
            roads,
            result.Heights,
            result.Buildable,
            settlement.PostOffice,
            settlement.Streets,
            settlement.Houses,
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm);
        int count = SmallIslandTiles * SmallIslandTiles;
        var occupied = new bool[count];
        WorldGrid.FillOccupied(
            occupied,
            SmallIslandTiles,
            SmallIslandTiles,
            settlement.PostOffice,
            settlement.Streets,
            settlement.Lots);
        var resourceNodes = ResourceStage.Place(
            resources,
            result.Heights,
            occupied,
            settlement.PostOffice,
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm);
        var walk = new bool[count];
        WorldGrid.FillWalkable(
            walk,
            result.Heights,
            result.Buildable,
            SmallIslandTiles,
            SmallIslandTiles,
            settlement.PostOffice,
            settlement.Streets);
        var spawnEdges = SpawnEdgeStage.Place(
            spawns,
            result.Heights,
            walk,
            settlement.PostOffice,
            settlement.Houses,
            connectivity.Ferries,
            SmallIslandTiles,
            SmallIslandTiles);
        return new WorldTables(
            SmallIslandTiles,
            SmallIslandTiles,
            SmallIslandTileCm,
            result.Heights,
            settlement.Houses,
            result.Buildable,
            result.Attempts,
            settlement.PostOffice,
            settlement.Streets,
            settlement.Lots,
            resourceNodes,
            connectivity.Ferries,
            connectivity.RouteNodes,
            connectivity.RouteEdges,
            spawnEdges,
            validationAttempts);
    }
}
