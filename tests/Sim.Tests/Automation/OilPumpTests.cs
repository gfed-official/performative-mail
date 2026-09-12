using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class OilPumpTests
{
    private static readonly TileCoord Origin = new(4, 4);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId GlassId = new(5);
    private static readonly ItemDefId OilCanId = new(6);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void RepoDefs_Pump_MatchesChapterCostsAndBpOil()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());
        var shop = Index(LoadShop());
        var items = Index(LoadItems());

        Assert.True(buildings.TryGetValue(OilPump.BuildingId, out var pump));
        Assert.True(recipes.TryGetValue(pump.Recipe, out var recipe));
        Assert.Equal("recipe_pump", pump.Recipe);
        Assert.Equal(OilPump.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(400, pump.Hp);
        Assert.Equal(1, pump.Footprint.W);
        Assert.Equal(1, pump.Footprint.H);
        Assert.Equal(4, pump.Rotations);
        Assert.False(pump.OnStreet);
        Assert.Equal(WaterPlacement.None, pump.OnWater);
        Assert.Equal(15, pump.MaxSlopeDeg);
        Assert.False(pump.DragLine);
        Assert.Equal(BuildingBehaviour.Pump, pump.Behaviour);
        Assert.Null(pump.Container);
        Assert.Equal("bp_oil", recipe.Blueprint);
        Assert.Equal(3, recipe.UnlockShift);
        Assert.Equal("iron_ingot", recipe.Inputs[0].Item);
        Assert.Equal(6, recipe.Inputs[0].Count);
        Assert.Equal("glass", recipe.Inputs[1].Item);
        Assert.Equal(2, recipe.Inputs[1].Count);

        Assert.True(shop.TryGetValue("bp_oil", out var row));
        Assert.Equal(ShopKind.Blueprint, row.Kind);
        Assert.Equal(600, row.Price);
        Assert.Equal(3, row.FromShift);
        Assert.True(row.OncePerRun);
        Assert.Equal("bp_oil", row.GrantBlueprint);

        Assert.True(items.TryGetValue(OilPump.OutputItemId, out var oilCan));
        Assert.Equal("Oil Can", oilCan.Name);
        Assert.Equal(StackCategory.Material, oilCan.Category);
        Assert.Contains("fuel", oilCan.Tags);
        Assert.Equal(TickClock.TicksFromSeconds(45), OilPump.OutputPeriodTicks);
        Assert.Equal(1350, OilPump.OutputPeriodTicks);
    }

    [Fact]
    public void Place_ConsumesRecipe_AndOccupiesOneTile()
    {
        var fx = Loaded(iron: 6, glass: 2);
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(OilPump.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(OilPump.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, IronId));
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, GlassId));
        Assert.True(fx.Registry.TryGetAt(Origin, out var at));
        Assert.Equal(placed.Construct.Id, at.Id);
        Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(OilPump.BuildingId, Origin, Facing.East, Owner));
    }

    [Fact]
    public void Place_StreetTile_Rejects()
    {
        var fx = Loaded(iron: 6, glass: 2, field: PlacementField.Flat(8, 6, 200).WithStreet(Origin));
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(OilPump.BuildingId, Origin, Facing.East, Owner));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(6, CountItem(fx.Inv, fx.Bag, IronId));
        Assert.Equal(2, CountItem(fx.Inv, fx.Bag, GlassId));
    }

    [Fact]
    public void StepTicks_45s_OutputsOneOilCan()
    {
        var fx = PlacedPump();
        fx.Pumps.StepTicks(OilPump.OutputPeriodTicks - 1);
        Assert.Equal(0, CountItem(fx.Inv, fx.Output, OilCanId));

        fx.Pumps.StepTicks(1);
        Assert.Equal(1, CountItem(fx.Inv, fx.Output, OilCanId));
        var stack = SingleStack(fx.Inv, fx.Output);
        Assert.Equal(OilCanId, stack.Item);
        Assert.Equal(OilPump.OutputCount, stack.Count);
        Assert.Equal("oil_can", OilPump.OutputItemId);
    }

    [Fact]
    public void StepTicks_90s_OutputsTwoOilCans()
    {
        var fx = PlacedPump();
        fx.Pumps.StepTicks(OilPump.OutputPeriodTicks * 2);
        Assert.Equal(2, CountItem(fx.Inv, fx.Output, OilCanId));
    }

    [Fact]
    public void StepTicks_FullOutput_HoldsProduction()
    {
        var fx = PlacedPump(output: new ContainerSpec(ContainerShape.Grid(1, 1), null));
        fx.Pumps.StepTicks(OilPump.OutputPeriodTicks);
        Assert.Equal(0, CountItem(fx.Inv, fx.Output, OilCanId));

        var room = fx.Inv.CreateContainer(ContainerSpec.Chest);
        fx.Pumps.BindOutput(Origin, room);
        fx.Pumps.StepTicks(1);
        Assert.Equal(1, CountItem(fx.Inv, room, OilCanId));
        Assert.Equal(0, CountItem(fx.Inv, fx.Output, OilCanId));
    }

    private static ItemStack SingleStack(InventorySystem inv, ContainerId container)
    {
        Assert.True(inv.TryGetContainer(container, out var grid));
        var entry = Assert.Single(grid.Entries);
        return Assert.IsType<ItemStack>(entry.Stack);
    }

    private static int CountItem(InventorySystem inv, ContainerId container, ItemDefId id)
    {
        Assert.True(inv.TryGetContainer(container, out var grid));
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is ItemStack item && item.Item.Equals(id))
                n += item.Count;
        }

        return n;
    }

    private static PumpFixture PlacedPump(ContainerSpec? output = null)
    {
        var fx = Loaded(iron: 6, glass: 2);
        Assert.IsType<Placed>(fx.Registry.TryPlace(OilPump.BuildingId, Origin, Facing.East, Owner));
        var dest = fx.Inv.CreateContainer(output ?? ContainerSpec.Chest);
        var pumps = new OilPumpNetwork();
        pumps.BindInventory(fx.Inv, Ids());
        pumps.BindOutput(Origin, dest);
        pumps.Compile(fx.Registry.All);
        Assert.Single(pumps.Pumps);
        return new PumpFixture(fx.Inv, dest, pumps);
    }

    private static Fixture Loaded(int iron = 0, int glass = 0, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        if (glass > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(GlassId, glass))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(8, 6, 200),
            inv,
            bag,
            Ids());
        return new Fixture(registry, inv, bag);
    }

    private static BuildingDef[] LoadBuildings() =>
        BuildingCatalog.LoadDir(Path.Combine(FindContentRoot(), BuildingCatalog.RelativeDir));

    private static RecipeDef[] LoadRecipes() =>
        RecipeCatalog.LoadDir(Path.Combine(FindContentRoot(), RecipeCatalog.RelativeDir));

    private static ShopItemDef[] LoadShop() =>
        ShopCatalog.LoadDir(Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir));

    private static ItemDef[] LoadItems() =>
        ItemCatalog.LoadDir(Path.Combine(FindContentRoot(), ItemCatalog.RelativeDir));

    private static Dictionary<string, T> Index<T>(T[] defs) where T : class
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            string id = def switch
            {
                BuildingDef building => building.Id,
                RecipeDef recipe => recipe.Id,
                ShopItemDef row => row.Id,
                ItemDef item => item.Id,
                _ => throw new InvalidOperationException(def.GetType().Name)
            };
            map.Add(id, def);
        }

        return map;
    }

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["iron_ingot"] = IronId,
        ["glass"] = GlassId,
        ["oil_can"] = OilCanId
    };

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

    private readonly record struct Fixture(ConstructRegistry Registry, InventorySystem Inv, ContainerId Bag);

    private readonly record struct PumpFixture(
        InventorySystem Inv,
        ContainerId Output,
        OilPumpNetwork Pumps);

    private sealed class MaterialCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.FootprintOf(key);
            if (key.Def == OilCanId.Value) return new Footprint(1, 2);
            return new Footprint(1, 1);
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.MaxStackOf(key);
            if (key.Def == OilCanId.Value) return 5;
            return 20;
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.WeightOf(key);
            return WeightClass.Light;
        }

        public StackCategory CategoryOf(StackKey key)
            => key.IsMail ? StackCategory.Mail : StackCategory.Material;
    }
}
