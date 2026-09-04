using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenRoadsTests
{
    public const uint FixedSeed = WorldGenHashTests.FixedSeed;

    [Fact]
    public void GenerateSmallIsland_FixedSeed_ValidWithResourcesRoutesAndSpawns()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.True(tables.Valid);
        Assert.Equal(50, tables.Houses.Length);
        int district1 = 0;
        foreach (var house in tables.Houses)
        {
            if (house.Address.District == 1)
                district1++;
        }

        Assert.InRange(district1, 8, 12);
        AssertResourceMins(tables);
        Assert.NotEmpty(tables.SpawnEdges);
        Assert.NotEmpty(tables.RouteNodes);
        Assert.NotEmpty(tables.RouteEdges);
        bool streetRun = false;
        foreach (var edge in tables.RouteEdges)
        {
            if (edge.Surface == ConnectivityStage.SurfaceStreet && edge.LengthTiles > 1)
                streetRun = true;
        }

        Assert.True(streetRun);
        Assert.Equal(1, tables.ValidationAttempts);
    }

    [Fact]
    public void GenerateSmallIsland_ResourceNodes_OutsideLotsStreetsAndPo()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        int count = tables.Width * tables.Height;
        var occ = new bool[count];
        WorldGrid.FillOccupied(
            occ,
            tables.Width,
            tables.Height,
            tables.PostOffice,
            tables.Streets,
            tables.Lots);
        foreach (var node in tables.ResourceNodes)
        {
            Assert.True(WorldGrid.InBounds(node.Tile.X, node.Tile.Y, tables.Width, tables.Height));
            int i = WorldGrid.Idx(node.Tile.X, node.Tile.Y, tables.Width);
            Assert.False(occ[i]);
            Assert.True(HeightmapStage.IsLand(tables.Heights[i]));
        }
    }

    [Fact]
    public void GenerateSmallIsland_SameSeed_SameNodesEdgesSpawns()
    {
        var a = WorldGen.GenerateSmallIsland(FixedSeed);
        var b = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.Equal(a.ResourceNodes, b.ResourceNodes);
        Assert.Equal(a.Ferries, b.Ferries);
        Assert.Equal(a.RouteNodes, b.RouteNodes);
        Assert.Equal(a.RouteEdges, b.RouteEdges);
        Assert.Equal(a.SpawnEdges.Length, b.SpawnEdges.Length);
        for (int i = 0; i < a.SpawnEdges.Length; i++)
        {
            Assert.Equal(a.SpawnEdges[i].District, b.SpawnEdges[i].District);
            Assert.Equal(a.SpawnEdges[i].Tile, b.SpawnEdges[i].Tile);
            Assert.Equal(a.SpawnEdges[i].PathToPo, b.SpawnEdges[i].PathToPo);
        }

        Assert.Equal(WorldHash.Compute(a), WorldHash.Compute(b));
    }

    [Fact]
    public void GenerateValidatedSmallIsland_HundredSeeds_AllValidAndFewRerolls()
    {
        var rng = new Pcg32(0xC0FFEEUL, 1UL);
        int rerolled = 0;
        for (int i = 0; i < 100; i++)
        {
            uint seed = rng.NextUInt32();
            var tables = WorldGen.GenerateValidatedSmallIsland(seed, out int rerolls);
            Assert.True(tables.Valid, $"seed=0x{seed:X8} valid={tables.Valid} rerolls={rerolls}");
            AssertResourceMins(tables);
            Assert.NotEmpty(tables.SpawnEdges);
            Assert.Equal(1 + rerolls, tables.ValidationAttempts);
            if (rerolls >= 1) rerolled++;
        }

        Assert.True(rerolled <= 5, $"reroll count {rerolled} exceeds 5");
    }

    private static void AssertResourceMins(WorldTables tables)
    {
        int wood = 0, fiber = 0, stone = 0, ore = 0, sand = 0, berries = 0;
        foreach (var node in tables.ResourceNodes)
        {
            switch (node.Kind)
            {
                case ResourceKind.Wood: wood++; break;
                case ResourceKind.Fiber: fiber++; break;
                case ResourceKind.Stone: stone++; break;
                case ResourceKind.IronOre: ore++; break;
                case ResourceKind.Sand: sand++; break;
                case ResourceKind.Berries: berries++; break;
            }
        }

        Assert.True(wood >= 6, $"wood={wood}");
        Assert.True(fiber >= 1, $"fiber={fiber}");
        Assert.True(stone >= 3, $"stone={stone}");
        Assert.True(ore >= 8, $"ore={ore}");
        Assert.True(sand >= 1, $"sand={sand}");
        Assert.True(berries >= 1, $"berries={berries}");
    }
}
