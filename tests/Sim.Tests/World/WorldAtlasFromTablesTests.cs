using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldAtlasFromTablesTests
{
    [Fact]
    public void FromTables_ArcadeSeed_KeepsHousesAndSpawnPad()
    {
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        var atlas = WorldAtlas.FromTables(tables);

        Assert.Equal(tables.TileCm, atlas.TileCm);
        Assert.Equal(tables.Houses.Length, atlas.Houses.Count);
        Assert.Equal(tables.PostOffice.SpawnPadTile, atlas.PostOffice.SpawnPadTile);
        Assert.True(atlas.Walkable(tables.PostOffice.SpawnPadTile));
        Assert.True(atlas.Houses.ContainsKey(tables.Houses[0].Address));
        Assert.Equal(tables.Houses.Length, atlas.DeliverableAddresses.Count);
    }

    [Fact]
    public void FromTables_EmptyHouses_Throws()
    {
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        var empty = new WorldTables(
            tables.Width,
            tables.Height,
            tables.TileCm,
            tables.Heights,
            Array.Empty<HouseRecord>(),
            tables.Buildable,
            tables.HeightmapAttempts,
            tables.PostOffice,
            tables.Streets,
            tables.Lots,
            tables.ResourceNodes,
            tables.Ferries,
            tables.RouteNodes,
            tables.RouteEdges,
            tables.SpawnEdges,
            tables.ValidationAttempts);
        Assert.Throws<ArgumentException>(() => WorldAtlas.FromTables(empty));
    }
}

public sealed class AddressTextTests
{
    [Fact]
    public void Format_UsesStreetNameAndNumber()
    {
        var streets = new[] { new StreetRecord(1, "Larch Lane", 1, Array.Empty<TileCoord>()) };
        Assert.Equal("13 Larch Lane", AddressText.Format(new AddressId(1, 1, 13, 0), streets));
        Assert.Equal("13 Larch Lane Unit 2", AddressText.Format(new AddressId(1, 1, 13, 2), streets));
    }

    [Fact]
    public void Format_UnknownStreet_FallsBackToNumber()
    {
        Assert.Equal("7", AddressText.Format(new AddressId(1, 9, 7, 0), Array.Empty<StreetRecord>()));
    }
}
