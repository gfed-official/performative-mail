using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldGenSettlementTests
{
    public const uint FixedSeed = WorldGenHashTests.FixedSeed;

    [Fact]
    public void GenerateSmallIsland_FixedSeed_AddressTableUniqueAndNonEmpty()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.NotEmpty(tables.Addresses);
        Assert.Equal(tables.Houses.Length, tables.Addresses.Length);

        var seen = new HashSet<uint>();
        foreach (var address in tables.Addresses)
        {
            Assert.True(seen.Add(address.Packed));
            Assert.NotEqual(0, address.District);
            Assert.NotEqual(0, address.Street);
            Assert.NotEqual(0, address.Number);
            Assert.Equal(0, address.Unit);
        }
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_PostOfficeIs6x6()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        var po = tables.PostOffice;
        Assert.Equal(new TileCoord(6, 6), po.SizeTiles);
        Assert.Equal(6, po.Footprint.Width);
        Assert.Equal(6, po.Footprint.Height);
        Assert.True(po.Footprint.Contains(po.SpawnPadTile));
        Assert.True(po.Footprint.Contains(po.IntakeTile));
        Assert.Equal(Facing.East, po.IntakeFace);
        Assert.InRange(po.Tile.X, 0, tables.Width - 6);
        Assert.InRange(po.Tile.Y, 0, tables.Height - 6);
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_District1HasPoAndEightToTwelveHouses()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        int district1 = 0;
        foreach (var house in tables.Houses)
        {
            if (house.Address.District == 1)
                district1++;
        }

        Assert.InRange(district1, 8, 12);
        Assert.True(tables.PostOffice.SizeTiles.X == 6);
        foreach (var house in tables.Houses)
            Assert.InRange(house.Address.District, (byte)1, (byte)255);
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_StreetNamesUniqueFromCatalog()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.NotEmpty(tables.Streets);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var street in tables.Streets)
        {
            Assert.False(string.IsNullOrWhiteSpace(street.Name));
            Assert.True(names.Add(street.Name));
        }

        var catalog = StreetCatalog.Load();
        foreach (var street in tables.Streets)
            Assert.Contains(street.Name, catalog);
    }

    [Fact]
    public void GenerateSmallIsland_FixedSeed_MailboxesOnLattice()
    {
        var tables = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.NotEmpty(tables.Houses);
        foreach (var house in tables.Houses)
            Assert.True(house.Mailbox.OnLattice(WorldAtlas.LatticeCm));
    }

    [Fact]
    public void GenerateSmallIsland_SameSeed_SameSettlement()
    {
        var a = WorldGen.GenerateSmallIsland(FixedSeed);
        var b = WorldGen.GenerateSmallIsland(FixedSeed);
        Assert.Equal(a.Addresses, b.Addresses);
        Assert.Equal(a.PostOffice, b.PostOffice);
        Assert.Equal(a.Houses.Length, b.Houses.Length);
        Assert.Equal(a.Streets.Length, b.Streets.Length);
        for (int i = 0; i < a.Streets.Length; i++)
        {
            Assert.Equal(a.Streets[i].Id, b.Streets[i].Id);
            Assert.Equal(a.Streets[i].Name, b.Streets[i].Name);
            Assert.Equal(a.Streets[i].District, b.Streets[i].District);
        }
    }

    [Fact]
    public void StreetCatalog_RepoFile_HasUniqueNames()
    {
        var names = StreetCatalog.Load();
        Assert.True(names.Length >= 120);
        Assert.Contains("Larch Lane", names);
        Assert.Contains("Saltmarsh Row", names);
    }
}
