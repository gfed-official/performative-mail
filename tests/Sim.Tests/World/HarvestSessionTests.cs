using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class HarvestSessionTests
{
    private static readonly TileCoord Origin = new(0, 0);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId FiberId = new(2);

    [Theory]
    [InlineData(ResourceKind.Wood, "log", 2, 5, HarvestRemnant.Stump)]
    [InlineData(ResourceKind.Fiber, "fiber", 3, 3, HarvestRemnant.RegrowNextShift)]
    [InlineData(ResourceKind.Stone, "stone", 3, 6, HarvestRemnant.Gone)]
    [InlineData(ResourceKind.IronOre, "iron_ore", 2, 8, HarvestRemnant.Gone)]
    [InlineData(ResourceKind.Sand, "sand", 4, 4, HarvestRemnant.RegrowNextShift)]
    [InlineData(ResourceKind.Berries, "berries", 2, 2, HarvestRemnant.RegrowNextShift)]
    public void Table_Chapter02_YieldHitsAndAfter(
        ResourceKind kind,
        string item,
        int yield,
        int hits,
        HarvestRemnant after)
    {
        var spec = HarvestTable.Of(kind);
        Assert.Equal(item, spec.ItemId);
        Assert.Equal(yield, spec.YieldPerHit);
        Assert.Equal(hits, spec.Hits);
        Assert.Equal(after, spec.After);
    }

    [Fact]
    public void Axe_OnWood_TwoLogFiveHitsStumpRemains()
    {
        var fx = Node(ResourceKind.Wood);

        Harvested? last = null;
        for (int i = 0; i < 5; i++)
        {
            last = Assert.IsType<Harvested>(fx.Session.Hit(Origin, HarvestTool.Axe));
            Assert.Equal(ResourceKind.Wood, last.Kind);
            Assert.Equal("log", last.Item);
            Assert.Equal(2, last.Count);
            Assert.Equal(4 - i, last.HitsLeft);
        }

        Assert.Equal(0, last!.HitsLeft);
        Assert.Equal(HarvestRemnant.Stump, last.Remnant);
        Assert.True(fx.Session.TryGet(Origin, out var state));
        Assert.Equal(0, state.HitsLeft);
        Assert.Equal(HarvestRemnant.Stump, state.Remnant);
        Assert.Equal(10, CountItem(fx, LogId));

        var again = Assert.IsType<HarvestRejected>(fx.Session.Hit(Origin, HarvestTool.Axe));
        Assert.Equal(HarvestReject.Exhausted, again.Reason);
        Assert.Equal(10, CountItem(fx, LogId));
    }

    [Fact]
    public void Hand_OnFiber_ThreeFiberThreeHitsRegrowFlag()
    {
        var fx = Node(ResourceKind.Fiber);

        Harvested? last = null;
        for (int i = 0; i < 3; i++)
        {
            last = Assert.IsType<Harvested>(fx.Session.Hit(Origin, HarvestTool.Hand));
            Assert.Equal("fiber", last.Item);
            Assert.Equal(3, last.Count);
            Assert.Equal(2 - i, last.HitsLeft);
        }

        Assert.Equal(HarvestRemnant.RegrowNextShift, last!.Remnant);
        Assert.True(fx.Session.TryGet(Origin, out var state));
        Assert.Equal(HarvestRemnant.RegrowNextShift, state.Remnant);
        Assert.Equal(9, CountItem(fx, FiberId));
    }

    [Fact]
    public void Hand_OnWood_HalfYield()
    {
        var session = new HarvestSession(new[] { new ResourceNodeRecord(ResourceKind.Wood, Origin) });

        var hit = Assert.IsType<Harvested>(session.Hit(Origin, HarvestTool.Hand));

        Assert.Equal("log", hit.Item);
        Assert.Equal(1, hit.Count);
        Assert.Equal(4, hit.HitsLeft);
        Assert.Equal(HarvestRemnant.Live, hit.Remnant);
    }

    [Fact]
    public void Pickaxe_OnWood_RejectedAndHitsUnchanged()
    {
        var session = new HarvestSession(new[] { new ResourceNodeRecord(ResourceKind.Wood, Origin) });

        var rejected = Assert.IsType<HarvestRejected>(session.Hit(Origin, HarvestTool.Pickaxe));

        Assert.Equal(HarvestReject.WrongTool, rejected.Reason);
        Assert.True(session.TryGet(Origin, out var state));
        Assert.Equal(5, state.HitsLeft);
        Assert.Equal(HarvestRemnant.Live, state.Remnant);
    }

    [Fact]
    public void Pickaxe_OnStone_ThreeStoneSixHitsGone()
    {
        var session = new HarvestSession(new[] { new ResourceNodeRecord(ResourceKind.Stone, Origin) });
        Harvested? last = null;
        for (int i = 0; i < 6; i++)
            last = Assert.IsType<Harvested>(session.Hit(Origin, HarvestTool.Pickaxe));

        Assert.Equal("stone", last!.Item);
        Assert.Equal(3, last.Count);
        Assert.Equal(0, last.HitsLeft);
        Assert.Equal(HarvestRemnant.Gone, last.Remnant);
    }

    [Fact]
    public void Pickaxe_OnIronOre_TwoOreEightHitsGone()
    {
        var session = new HarvestSession(new[] { new ResourceNodeRecord(ResourceKind.IronOre, Origin) });
        Harvested? last = null;
        for (int i = 0; i < 8; i++)
            last = Assert.IsType<Harvested>(session.Hit(Origin, HarvestTool.Pickaxe));

        Assert.Equal("iron_ore", last!.Item);
        Assert.Equal(2, last.Count);
        Assert.Equal(HarvestRemnant.Gone, last.Remnant);
    }

    [Fact]
    public void Shovel_OnSand_FourSandFourHitsRegrow()
    {
        var session = new HarvestSession(new[] { new ResourceNodeRecord(ResourceKind.Sand, Origin) });
        Harvested? last = null;
        for (int i = 0; i < 4; i++)
            last = Assert.IsType<Harvested>(session.Hit(Origin, HarvestTool.Shovel));

        Assert.Equal("sand", last!.Item);
        Assert.Equal(4, last.Count);
        Assert.Equal(HarvestRemnant.RegrowNextShift, last.Remnant);
    }

    [Fact]
    public void Hand_OnBerries_TwoBerriesTwoHitsRegrow()
    {
        var session = new HarvestSession(new[] { new ResourceNodeRecord(ResourceKind.Berries, Origin) });
        Harvested? last = null;
        for (int i = 0; i < 2; i++)
            last = Assert.IsType<Harvested>(session.Hit(Origin, HarvestTool.Hand));

        Assert.Equal("berries", last!.Item);
        Assert.Equal(2, last.Count);
        Assert.Equal(HarvestRemnant.RegrowNextShift, last.Remnant);
    }

    [Fact]
    public void Hit_UnknownTile_Rejected()
    {
        var session = new HarvestSession(Array.Empty<ResourceNodeRecord>());
        var rejected = Assert.IsType<HarvestRejected>(session.Hit(Origin, HarvestTool.Axe));
        Assert.Equal(HarvestReject.UnknownNode, rejected.Reason);
    }

    [Fact]
    public void Hit_NoRoom_DoesNotSpendHit()
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var dest = inv.CreateContainer(new ContainerSpec(ContainerShape.Grid(1, 1), null));
        var session = new HarvestSession(
            new[] { new ResourceNodeRecord(ResourceKind.Wood, Origin) },
            inv,
            dest,
            Ids());

        var rejected = Assert.IsType<HarvestRejected>(session.Hit(Origin, HarvestTool.Axe));

        Assert.Equal(HarvestReject.NoRoom, rejected.Reason);
        Assert.True(session.TryGet(Origin, out var state));
        Assert.Equal(5, state.HitsLeft);
        Assert.True(inv.TryGetContainer(dest, out var grid));
        Assert.Empty(grid.Entries);
    }

    [Fact]
    public void GenerateSmallIsland_WoodNode_AxeYieldsTwoLog()
    {
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        ResourceNodeRecord? wood = null;
        foreach (var node in tables.ResourceNodes)
        {
            if (node.Kind != ResourceKind.Wood) continue;
            wood = node;
            break;
        }

        Assert.True(wood.HasValue);
        var session = new HarvestSession(tables.ResourceNodes);
        var hit = Assert.IsType<Harvested>(session.Hit(wood.Value.Tile, HarvestTool.Axe));

        Assert.Equal("log", hit.Item);
        Assert.Equal(2, hit.Count);
        Assert.Equal(4, hit.HitsLeft);
        Assert.Equal(HarvestRemnant.Live, hit.Remnant);
    }

    [Fact]
    public void Table_ItemIds_ExistInRepoItems()
    {
        var items = ItemCatalog.LoadDir(Path.Combine(FindContentRoot(), ItemCatalog.RelativeDir));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
            ids.Add(item.Id);

        foreach (var kind in new[]
        {
            ResourceKind.Wood,
            ResourceKind.Fiber,
            ResourceKind.Stone,
            ResourceKind.IronOre,
            ResourceKind.Sand,
            ResourceKind.Berries
        })
            Assert.Contains(HarvestTable.Of(kind).ItemId, ids);
    }

    private static Fixture Node(ResourceKind kind)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var dest = inv.CreateContainer(ContainerSpec.Chest);
        var session = new HarvestSession(
            new[] { new ResourceNodeRecord(kind, Origin) },
            inv,
            dest,
            Ids());
        return new Fixture(session, inv, dest);
    }

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["log"] = LogId,
        ["fiber"] = FiberId,
        ["stone"] = new ItemDefId(3),
        ["iron_ore"] = new ItemDefId(4),
        ["sand"] = new ItemDefId(5),
        ["berries"] = new ItemDefId(6)
    };

    private static int CountItem(Fixture fx, ItemDefId id)
    {
        Assert.True(fx.Inv.TryGetContainer(fx.Dest, out var grid));
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is ItemStack item && item.Item.Equals(id))
                n += item.Count;
        }

        return n;
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, ArchetypeCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/archetypes.json");
    }

    private readonly record struct Fixture(HarvestSession Session, InventorySystem Inv, ContainerId Dest);

    private sealed class MaterialCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return key.Def == LogId.Value ? new Footprint(1, 2) : new Footprint(1, 1);
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return key.Def == LogId.Value ? 10 : 20;
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return WeightClass.Light;
        }

        public StackCategory CategoryOf(StackKey key) => StackCategory.Material;
    }
}
