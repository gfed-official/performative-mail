using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenHashTests
{
    public const uint FixedSeed = 0x7F3A9C21;

    public const ulong U11SkeletonWorldHash = 0x24849E8EF0DBB228UL;

    public const ulong GoldenWorldHash = 0x936BE960EC16395AUL;

    [Fact]
    public void GenerateSmallIsland_SameSeed_SameWorldHash()
    {
        var a = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        var b = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        Assert.Equal(a, b);
    }

    [Fact]
    public void GenerateSmallIsland_AdjacentSeeds_DifferentWorldHash()
    {
        var a = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        var b = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed + 1));
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_GoldenWorldHash()
    {
        var hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        Assert.Equal(GoldenWorldHash, hash);
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_IntegerTables()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.Equal(WorldGen.SmallIslandTiles, tables.Width);
        Assert.Equal(WorldGen.SmallIslandTiles, tables.Height);
        Assert.Equal(WorldGen.SmallIslandTileCm, tables.TileCm);
        Assert.Equal(WorldGen.SmallIslandTiles * WorldGen.SmallIslandTiles, tables.Heights.Length);
        Assert.Empty(tables.Addresses);
        Assert.Equal(tables.Heights.Length, tables.Buildable.Length);
        foreach (var height in tables.Heights)
            Assert.InRange(height, short.MinValue, short.MaxValue);
    }

    [Fact]
    public void GenerateSmallIsland_ReplacesU11SkeletonHash()
    {
        var hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        Assert.NotEqual(U11SkeletonWorldHash, hash);
        Assert.Equal(GoldenWorldHash, hash);
    }

    [Fact]
    public void RngStream_StageNames_Diverge()
    {
        uint heightmap = RngStream.Derive(FixedSeed, "heightmap").NextUInt32();
        uint towns = RngStream.Derive(FixedSeed, "towns").NextUInt32();
        Assert.NotEqual(heightmap, towns);
    }

    [Fact]
    public void WorldHash_OneCentimetre_Changes()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        ulong before = WorldHash.Compute(tables);
        tables.Heights[0] += 1;
        ulong after = WorldHash.Compute(tables);
        Assert.NotEqual(before, after);
    }
}
