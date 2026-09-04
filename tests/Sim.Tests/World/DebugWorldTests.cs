using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class DebugWorldTests
{
    [Fact]
    public void Tables_SameCall_SameWorldHash()
    {
        ulong a = WorldHash.Compute(DebugWorld.Tables());
        ulong b = WorldHash.Compute(DebugWorld.Tables());
        Assert.Equal(a, b);
    }

    [Fact]
    public void Tables_PinsLayoutAndLabels()
    {
        var tables = DebugWorld.Tables();
        Assert.Equal(DebugWorld.Width, tables.Width);
        Assert.Equal(DebugWorld.Height, tables.Height);
        Assert.Equal(DebugWorld.TileCm, tables.TileCm);
        Assert.Equal(DebugWorld.PostOfficeTile, tables.PostOffice.Tile);
        Assert.Equal(DebugWorld.PostOfficeSize, tables.PostOffice.SizeTiles);
        Assert.Equal(DebugWorld.SpawnPadTile, tables.PostOffice.SpawnPadTile);
        Assert.Equal(DebugWorld.IntakeTile, tables.PostOffice.IntakeTile);
        Assert.Equal(Facing.East, tables.PostOffice.IntakeFace);
        Assert.Equal(2, tables.Houses.Length);
        Assert.Equal(DebugWorld.House1Mailbox, tables.Houses[0].Mailbox);
        Assert.Equal(DebugWorld.House2Mailbox, tables.Houses[1].Mailbox);
        Assert.Equal("1 Debug Lane", AddressText.Format(tables.Houses[0].Address, tables.Streets));
        Assert.Equal("2 Debug Lane", AddressText.Format(tables.Houses[1].Address, tables.Streets));
        Assert.Equal(DebugWorld.StreetName, tables.Streets[0].Name);
        Assert.False(tables.Valid);
    }

    [Fact]
    public void Tables_FromTables_KeepsSpawnPadWalkable()
    {
        var tables = DebugWorld.Tables();
        var atlas = WorldAtlas.FromTables(tables);
        Assert.Equal(2, atlas.Houses.Count);
        Assert.True(atlas.Walkable(tables.PostOffice.SpawnPadTile));
    }

    [Fact]
    public void Tables_WorldHash_IsStableAndNotGoldenSmallIsland()
    {
        ulong hash = WorldHash.Compute(DebugWorld.Tables());
        Assert.NotEqual(WorldGenHashTests.GoldenWorldHash, hash);
        Assert.Equal(DebugWorld.Hash, hash);
    }
}
