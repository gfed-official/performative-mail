using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenHashTests
{
    public const uint FixedSeed = 0x7F3A9C21;

    // U1.1 hashed a PCG stub fill. U1.2 replaces that field with OpenSimplex2 +
    // coastline + falloff, so the golden must move. Kept to prove this is not a rename.
    public const ulong U11SkeletonWorldHash = 0x24849E8EF0DBB228UL;

    // Seed 0x7F3A9C21 after the U1.2 height field and empty addresses.
    public const ulong U12HeightmapWorldHash = 0x936BE960EC16395AUL;

    // Seed 0x7F3A9C21 after U1.3 fills the address table. Not 0x936BE960EC16395A.
    public const ulong GoldenWorldHash = 0x631CE9A07B6A504FUL;

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
        Assert.NotEmpty(tables.Addresses);
        Assert.Equal(tables.Heights.Length, tables.Buildable.Length);
        foreach (var height in tables.Heights)
            Assert.InRange(height, short.MinValue, short.MaxValue);
    }

    [Fact]
    public void GenerateSmallIsland_ReplacesU11SkeletonHash()
    {
        var hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        Assert.NotEqual(U11SkeletonWorldHash, hash);
        Assert.NotEqual(U12HeightmapWorldHash, hash);
        Assert.Equal(GoldenWorldHash, hash);
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_ReplacesU12HeightmapHash()
    {
        var hash = WorldHash.Compute(WorldGen.GenerateSmallIsland(FixedSeed));
        Assert.NotEqual(U12HeightmapWorldHash, hash);
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
