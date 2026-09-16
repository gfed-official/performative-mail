using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Tests.World;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class ReferenceFactoryTests
{
    private const int BeltRows = 5;
    private const int BeltCols = 8;
    private const int BeltCount = BeltRows * BeltCols;
    private const int SorterCount = 1;
    private const int InserterCount = 4;
    private const int PlankBuys = 3;
    private const int IronBuys = 6;
    private const int StoneHarvestMin = 12;
    private const int PlankShopMin = 20;
    private const int IronShopMin = 40;
    private const int StepTicks = 8;
    private const float InsertMetres = 0.25f;

    private static readonly TileCoord BeltOrigin = new(170, 140);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly int PickaxeSwingTicks = TickClock.TickHz * 3 / 5;
    private static readonly int PlaceInteractTicks = TickClock.TickHz * 2 / 5;
    private static readonly int ShopBuyTicks = TickClock.TickHz;

    [Fact]
    public void SmallIsland_ShopAndHarvest_PlacesAndRunsWithinPrepPlusDelivery()
    {
        var bundle = ContentFiles.Load(FindContentRoot());
        var ids = ContentIdMap.Build(bundle);
        var catalog = ContentStackCatalog.From(bundle, ids);
        Assert.True(ids.TryItem("plank", out var plankId));
        Assert.True(ids.TryItem("iron_ingot", out var ironId));
        Assert.True(ids.TryItem("stone", out var stoneId));

        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        var layout = LayoutAt(BeltOrigin);
        var bill = BillOf(layout, bundle.Buildings, bundle.Recipes);
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Depot);
        var registry = new ConstructRegistry(
            bundle.Buildings,
            bundle.Recipes,
            PlacementField.FromWorld(tables),
            inv,
            bag,
            ids.Items);
        var wallet = new Wallet(new Cents(3000));
        var shop = new ShopSession(bundle.Shop, wallet, WorldGenHashTests.FixedSeed, inv, bag, ids.Items);
        var harvest = new HarvestSession(tables.ResourceNodes, inv, bag, ids.Items);
        var clock = new ShiftClock(bundle.Balance, RunState.InLobby());
        Assert.True(clock.TryEnter(RunPhase.Generating));
        Assert.True(clock.TryEnter(RunPhase.Prep));
        int prepTicks = ShiftDurations.Ticks(RunPhase.Prep, 1, bundle.Balance);
        int deliveryTicks = ShiftDurations.Ticks(RunPhase.Delivery, 1, bundle.Balance);

        shop.RollOffers(1, RunPhase.Prep);
        int plankGranted = Buy(shop, clock, "plank_x20", PlankBuys);
        int ironGranted = Buy(shop, clock, "iron_ingot_x10", IronBuys);
        Assert.True(plankGranted >= PlankShopMin);
        Assert.True(ironGranted >= IronShopMin);

        int stoneGranted = 0;
        var stoneTile = FirstStone(tables);
        while (stoneGranted < StoneHarvestMin)
        {
            var hit = Assert.IsType<Harvested>(harvest.Hit(stoneTile, HarvestTool.Pickaxe));
            stoneGranted += hit.Count;
            Charge(clock, PickaxeSwingTicks);
        }

        Assert.True(stoneGranted >= StoneHarvestMin);
        int plankBefore = CountItem(inv, bag, plankId);
        int ironBefore = CountItem(inv, bag, ironId);
        int stoneBefore = CountItem(inv, bag, stoneId);

        foreach (var row in layout)
        {
            Assert.IsType<Placed>(registry.TryPlace(row.DefId, row.Tile, row.Rotation, Owner));
            Charge(clock, PlaceInteractTicks);
        }

        Assert.Equal(plankBefore - bill.Plank, CountItem(inv, bag, plankId));
        Assert.Equal(ironBefore - bill.IronIngot, CountItem(inv, bag, ironId));
        Assert.Equal(stoneBefore - bill.Stone, CountItem(inv, bag, stoneId));
        Assert.Equal(40, CountDef(registry, BeltNetwork.BuildingId));
        Assert.Equal(1, CountDef(registry, AddressSorter.BuildingId));
        Assert.Equal(4, CountDef(registry, Inserter.BuildingId));
        Assert.Equal(44, bill.Plank);
        Assert.Equal(52, bill.IronIngot);
        Assert.Equal(6, bill.Stone);

        var belts = new BeltNetwork();
        belts.Compile(registry.All);
        var sorters = new AddressSorterNetwork();
        sorters.Compile(registry.All, belts);
        var inserters = new InserterNetwork();
        inserters.Compile(registry.All, belts);
        Assert.Single(sorters.Sorters);
        Assert.Equal(4, inserters.Inserters.Count);

        var machine = Assert.Single(sorters.Sorters);
        var input = SegmentEndingAt(belts, machine.Ports.Input);
        Assert.True(input.TryInsert(0, 1, InsertMetres));
        float before = Assert.Single(input.Lane(0)).MetresFromStart;
        Assert.Equal(InsertMetres, before);
        belts.StepTicks(StepTicks);
        Assert.Equal(1, Assert.Single(input.Lane(0)).ItemId);
        Assert.True(Assert.Single(input.Lane(0)).MetresFromStart > InsertMetres);

        Assert.True(clock.Now > 0);
        Assert.True(clock.Now <= (uint)(prepTicks + deliveryTicks));
        Assert.True(clock.State.Phase is RunPhase.Prep or RunPhase.Delivery);
    }

    private static FactoryTile[] LayoutAt(TileCoord origin)
    {
        var rows = new FactoryTile[BeltCount + SorterCount + InserterCount];
        int n = 0;
        for (int y = 0; y < BeltRows; y++)
        {
            for (int x = 0; x < BeltCols; x++)
                rows[n++] = new FactoryTile(BeltNetwork.BuildingId, new TileCoord(origin.X + x, origin.Y + y), Facing.East);
        }

        rows[n++] = new FactoryTile(
            AddressSorter.BuildingId,
            new TileCoord(origin.X + BeltCols, origin.Y + 1),
            Facing.East);
        for (int i = 0; i < InserterCount; i++)
            rows[n++] = new FactoryTile(Inserter.BuildingId, new TileCoord(origin.X - 1, origin.Y + i), Facing.East);
        Assert.Equal(rows.Length, n);
        return rows;
    }

    private static MaterialBill BillOf(
        IReadOnlyList<FactoryTile> layout,
        BuildingDef[] buildings,
        RecipeDef[] recipes)
    {
        var byBuilding = IndexBuildings(buildings);
        var byRecipe = IndexRecipes(recipes);
        int plank = 0;
        int iron = 0;
        int stone = 0;
        foreach (var row in layout)
        {
            var building = byBuilding[row.DefId];
            var recipe = byRecipe[building.Recipe];
            foreach (var input in recipe.Inputs)
            {
                switch (input.Item)
                {
                    case "plank":
                        plank += input.Count;
                        break;
                    case "iron_ingot":
                        iron += input.Count;
                        break;
                    case "stone":
                        stone += input.Count;
                        break;
                    default:
                        throw new InvalidOperationException(input.Item);
                }
            }
        }

        return new MaterialBill(plank, iron, stone);
    }

    private static Dictionary<string, BuildingDef> IndexBuildings(BuildingDef[] buildings)
    {
        var map = new Dictionary<string, BuildingDef>(StringComparer.Ordinal);
        foreach (var building in buildings)
            map.Add(building.Id, building);
        return map;
    }

    private static Dictionary<string, RecipeDef> IndexRecipes(RecipeDef[] recipes)
    {
        var map = new Dictionary<string, RecipeDef>(StringComparer.Ordinal);
        foreach (var recipe in recipes)
            map.Add(recipe.Id, recipe);
        return map;
    }

    private static int Buy(ShopSession shop, ShiftClock clock, string sku, int times)
    {
        int granted = 0;
        for (int i = 0; i < times; i++)
        {
            var bought = Assert.IsType<ShopBought>(shop.TryBuy(sku));
            granted += bought.Count;
            Charge(clock, ShopBuyTicks);
        }

        return granted;
    }

    private static void Charge(ShiftClock clock, int ticks)
        => clock.AdvanceTo(clock.Now + (uint)ticks);

    private static BeltSegment SegmentEndingAt(BeltNetwork belts, TileCoord ahead)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            if (belts.Segments[i].AheadTile.Equals(ahead))
                return belts.Segments[i];
        }

        throw new InvalidOperationException("missing input segment");
    }

    private static TileCoord FirstStone(WorldTables tables)
    {
        foreach (var node in tables.ResourceNodes)
        {
            if (node.Kind == ResourceKind.Stone)
                return node.Tile;
        }

        throw new InvalidOperationException("no stone node");
    }

    private static int CountDef(ConstructRegistry registry, string defId)
    {
        int n = 0;
        foreach (var row in registry.All)
        {
            if (string.Equals(row.DefId, defId, StringComparison.Ordinal))
                n++;
        }

        return n;
    }

    private static int CountItem(InventorySystem inv, ContainerId bag, ItemDefId id)
    {
        Assert.True(inv.TryGetContainer(bag, out var grid));
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

    private readonly record struct FactoryTile(string DefId, TileCoord Tile, Facing Rotation);

    private readonly record struct MaterialBill(int Plank, int IronIngot, int Stone);
}
