using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenHeightmapTests
{
    public const uint FixedSeed = WorldGenHashTests.FixedSeed;

    [Fact]
    public void GenerateSmallIsland_SameSeed_SameHeightField()
    {
        var a = WorldGen.GenerateSmallIsland(FixedSeed);
        var b = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.Equal(a.Heights, b.Heights);
        Assert.Equal(a.Buildable, b.Buildable);
        Assert.Equal(a.Valid, b.Valid);
        Assert.Equal(a.HeightmapAttempts, b.HeightmapAttempts);
    }

    [Fact]
    public void GenerateSmallIsland_SeaLevelZero_HasLandAndWater()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        int sea = 0;
        int land = 0;
        foreach (var height in tables.Heights)
        {
            if (height <= HeightmapStage.SeaLevelCm) sea++;
            else land++;
        }

        Assert.True(sea > 0);
        Assert.True(land > 0);
        Assert.True(tables.Valid);
        Assert.InRange(tables.HeightmapAttempts, 1, HeightmapStage.MaxAttempts);
    }

    [Fact]
    public void GenerateSmallIsland_Falloff_CornersAreSea()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        int w = tables.Width;
        int last = w - 1;
        Assert.True(tables.Heights[0] <= HeightmapStage.SeaLevelCm);
        Assert.True(tables.Heights[last] <= HeightmapStage.SeaLevelCm);
        Assert.True(tables.Heights[last * w] <= HeightmapStage.SeaLevelCm);
        Assert.True(tables.Heights[last * w + last] <= HeightmapStage.SeaLevelCm);
        Assert.True(tables.Heights[(w / 2) * w + (w / 2)] > HeightmapStage.SeaLevelCm);
    }

    [Fact]
    public void GenerateSmallIsland_NoSingleTileSpits()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        for (int y = 0; y < tables.Height; y++)
        {
            for (int x = 0; x < tables.Width; x++)
            {
                if (!HeightmapStage.IsLand(tables.Heights[y * tables.Width + x])) continue;
                Assert.True(HeightmapStage.LandNeighbors(tables.Heights, tables.Width, tables.Height, x, y) >= 2);
            }
        }
    }

    [Fact]
    public void GenerateSmallIsland_BuildableQuotaOrInvalid()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        bool quota = HeightmapStage.MeetsBuildableQuota(tables.Heights, tables.Buildable);
        Assert.Equal(quota, tables.Valid);
        Assert.True(tables.HeightmapAttempts <= HeightmapStage.MaxAttempts);
        if (tables.Valid)
            Assert.True(quota);
    }

    [Fact]
    public void HeightmapStage_QuotaAlwaysFails_StopsAtEightAttempts()
    {
        var stream = RngStream.Derive(FixedSeed, "heightmap");
        var result = HeightmapStage.Generate(
            stream,
            48,
            48,
            WorldGen.SmallIslandTileCm,
            (_, _) => false);
        Assert.False(result.Valid);
        Assert.Equal(HeightmapStage.MaxAttempts, result.Attempts);
    }

    [Fact]
    public void SmoothCoastline_RemovesOneTileSpit()
    {
        short land = 200;
        var heights = new short[]
        {
            0, land, 0,
            0, land, 0,
            0, 0, 0,
        };
        HeightmapStage.SmoothCoastline(heights, 3, 3);
        Assert.Equal(HeightmapStage.SeaLevelCm, heights[1]);
        Assert.Equal(HeightmapStage.SeaLevelCm, heights[4]);
    }
}

public sealed class OpenSimplex2FixedTests
{
    [Fact]
    public void Noise2_SameInput_SameOutput()
    {
        int a = OpenSimplex2Fixed.Noise2(1, 12 << 10, 34 << 10);
        int b = OpenSimplex2Fixed.Noise2(1, 12 << 10, 34 << 10);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Noise2_DifferentSeed_UsuallyDiverges()
    {
        int a = OpenSimplex2Fixed.Noise2(1, 12 << 10, 34 << 10);
        int b = OpenSimplex2Fixed.Noise2(2, 12 << 10, 34 << 10);
        Assert.NotEqual(a, b);
    }
}
