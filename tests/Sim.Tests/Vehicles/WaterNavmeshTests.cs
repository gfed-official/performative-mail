using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Tests.World;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class WaterNavmeshTests
{
    [Fact]
    public void Build_IncludesFerryLaneAndCoarseDeepWater()
    {
        var heights = Channel();
        var ferries = new[] { new FerryLaneRecord(new TileCoord(2, 4), new TileCoord(9, 4)) };

        var graph = WaterNavmesh.Build(heights, 12, 8, ferries);

        Assert.True(graph.TryNearestNode(new TileCoord(4, 4), out int deep));
        Assert.True(graph.TryNearestNode(new TileCoord(2, 4), out int beach));
        Assert.True(graph.TryPath(new TileCoord(2, 4), new TileCoord(9, 4), out var ferry));
        Assert.True(ferry.LengthTiles > 0);
        Assert.True(graph.TryPath(new TileCoord(4, 4), new TileCoord(8, 4), out var water));
        Assert.True(water.LengthTiles > 0);
        Assert.NotEqual(deep, beach);
    }

    [Fact]
    public void Build_SameHeights_SamePaths()
    {
        var heights = Channel();
        var ferries = new[] { new FerryLaneRecord(new TileCoord(2, 4), new TileCoord(9, 4)) };

        var a = WaterNavmesh.Build(heights, 12, 8, ferries);
        var b = WaterNavmesh.Build(heights, 12, 8, ferries);

        Assert.True(a.TryPath(new TileCoord(2, 4), new TileCoord(9, 4), out var pa));
        Assert.True(b.TryPath(new TileCoord(2, 4), new TileCoord(9, 4), out var pb));
        Assert.Equal(pa.LengthTiles, pb.LengthTiles);
        Assert.Equal(pa.Tiles, pb.Tiles);
    }

    [Fact]
    public void Build_GeneratedSmallIsland_IsDeterministicAndKeepsFerries()
    {
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        var a = WaterNavmesh.Build(tables);
        var b = WaterNavmesh.Build(tables);

        if (tables.Ferries.Length == 0)
            return;

        var lane = tables.Ferries[0];
        Assert.True(a.TryPath(lane.A, lane.B, out var pa));
        Assert.True(b.TryPath(lane.A, lane.B, out var pb));
        Assert.Equal(pa.LengthTiles, pb.LengthTiles);
        Assert.Equal(pa.Tiles, pb.Tiles);
    }

    private static short[] Channel()
    {
        var heights = new short[12 * 8];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 12; x++)
                heights[y * 12 + x] = x <= 2 || x >= 9 ? (short)100 : (short)-200;
        }

        return heights;
    }
}
