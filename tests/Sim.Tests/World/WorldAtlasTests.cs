using System.Text.Json;
using System.Text.Json.Nodes;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class WorldAtlasTests
{
    [Fact]
    public void Load_RepoTestMap_HasLarchLaneAndTenAddresses()
    {
        var path = FindRepoMap();
        Assert.EndsWith(Path.Combine("content", "world", "m0_test_map.json"), path);
        var atlas = WorldAtlasLoader.LoadFile(path);

        Assert.Equal("m0_test", atlas.Id);
        Assert.Equal(1, atlas.DistrictId);
        Assert.Equal(1, atlas.StreetId);
        Assert.Equal("Larch Lane", atlas.StreetName);
        Assert.Equal(WorldAtlas.TileCmDefault, atlas.TileCm);
        Assert.Equal(10, atlas.Houses.Count);
        Assert.Equal(10, atlas.DeliverableAddresses.Count);

        var numbers = new HashSet<int>();
        foreach (var address in atlas.DeliverableAddresses)
        {
            Assert.Equal(1, address.District);
            Assert.Equal(1, address.Street);
            Assert.Equal(0, address.Unit);
            Assert.InRange(address.Number, 1, 10);
            Assert.True(numbers.Add(address.Number));
            Assert.True(atlas.Houses.ContainsKey(address));
        }

        Assert.Equal(10, numbers.Count);
        for (byte n = 1; n <= 10; n++)
            Assert.Contains(new AddressId(1, 1, n, 0), atlas.DeliverableAddresses);
    }

    [Fact]
    public void Load_RepoTestMap_OddsAndEvensSitOnOppositeSides()
    {
        var atlas = LoadRepoAtlas();
        int? oddY = null;
        int? evenY = null;
        foreach (var house in atlas.Houses.Values)
        {
            if ((house.Address.Number & 1) == 1)
            {
                oddY ??= house.LotTile.Y;
                Assert.Equal(oddY.Value, house.LotTile.Y);
                Assert.True(house.LotTile.Y >= atlas.StreetRect.MaxY);
            }
            else
            {
                evenY ??= house.LotTile.Y;
                Assert.Equal(evenY.Value, house.LotTile.Y);
                Assert.True(house.Lot.MaxY <= atlas.StreetRect.Y);
            }
        }

        Assert.NotNull(oddY);
        Assert.NotNull(evenY);
        Assert.NotEqual(oddY, evenY);
    }

    [Fact]
    public void Load_RepoTestMap_PostOfficeIs6x6WithEastIntake()
    {
        var atlas = LoadRepoAtlas();
        var po = atlas.PostOffice;
        Assert.Equal(new TileCoord(0, 0), po.Tile);
        Assert.Equal(new TileCoord(6, 6), po.SizeTiles);
        Assert.Equal(6, po.Footprint.Width);
        Assert.Equal(6, po.Footprint.Height);
        Assert.True(po.Footprint.Contains(po.SpawnPadTile));
        Assert.True(po.Footprint.Contains(po.IntakeTile));
        Assert.Equal(Facing.East, po.IntakeFace);
        Assert.Equal(new TileCoord(2, 2), po.SpawnPadTile);
        Assert.Equal(new TileCoord(5, 2), po.IntakeTile);
    }

    [Fact]
    public void TryMailboxPose_EveryDeliverableAddress_IsLatticeAndReachesStreet()
    {
        var atlas = LoadRepoAtlas();
        foreach (var address in atlas.DeliverableAddresses)
        {
            Assert.True(atlas.TryMailboxPose(address, out var pose));
            Assert.True(pose.OnLattice(WorldAtlas.LatticeCm));
            Assert.True(atlas.MailboxReachesStreet(pose));
            var tile = pose.Tile(atlas.TileCm);
            Assert.True(atlas.Walkable(tile) || SharesStreetEdge(atlas, tile));
        }

        Assert.False(atlas.TryMailboxPose(new AddressId(1, 1, 11, 0), out _));
    }

    [Fact]
    public void Walkable_StreetAndSpawnPad_True_LotInterior_False()
    {
        var atlas = LoadRepoAtlas();
        Assert.True(atlas.Walkable(atlas.PostOffice.SpawnPadTile));
        Assert.True(atlas.Walkable(new TileCoord(atlas.StreetRect.X, atlas.StreetRect.Y)));
        Assert.True(atlas.Walkable(new TileCoord(atlas.StreetRect.MaxX - 1, atlas.StreetRect.MaxY - 1)));
        Assert.False(atlas.Walkable(new TileCoord(atlas.StreetRect.MaxX, atlas.StreetRect.Y)));

        var house1 = atlas.Houses[new AddressId(1, 1, 1, 0)];
        var interior = new TileCoord(house1.LotTile.X + 1, house1.LotTile.Y + 2);
        Assert.True(house1.Lot.Contains(interior));
        Assert.NotEqual(house1.Mailbox.Tile(atlas.TileCm), interior);
        Assert.False(atlas.Walkable(interior));
        Assert.False(atlas.Walkable(new TileCoord(1, 1)));
    }

    [Fact]
    public void Loader_MissingHouse_Throws()
    {
        var json = MutateRepoMap(houses => houses.RemoveAt(houses.Count - 1));
        var ex = Assert.Throws<WorldAtlasException>(() => WorldAtlasLoader.LoadJson(json, "missing-house"));
        Assert.Contains("expected 10 houses", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Loader_DuplicateNumber_Throws()
    {
        var json = MutateRepoMap(houses =>
        {
            houses[2]!["number"] = houses[0]!["number"]!.GetValue<int>();
        });
        var ex = Assert.Throws<WorldAtlasException>(() => WorldAtlasLoader.LoadJson(json, "duplicate-number"));
        Assert.Contains("duplicate house number", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadFromContentRoot_MatchesRepoFile()
    {
        var path = FindRepoMap();
        var root = Directory.GetParent(path)!.Parent!.FullName;
        var atlas = WorldAtlasLoader.LoadFromContentRoot(root);
        Assert.Equal(10, atlas.DeliverableAddresses.Count);
        Assert.Equal("Larch Lane", atlas.StreetName);
    }

    private static WorldAtlas LoadRepoAtlas() => WorldAtlasLoader.LoadFile(FindRepoMap());

    private static bool SharesStreetEdge(WorldAtlas atlas, TileCoord tile)
    {
        foreach (var neighbor in tile.EdgeNeighbors())
        {
            if (atlas.StreetRect.Contains(neighbor)) return true;
        }

        return false;
    }

    private static string MutateRepoMap(Action<JsonArray> mutateHouses)
    {
        var node = JsonNode.Parse(File.ReadAllText(FindRepoMap()))!;
        var houses = node["houses"]!.AsArray();
        mutateHouses(houses);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string FindRepoMap()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content", "world", "m0_test_map.json");
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/m0_test_map.json");
    }
}
