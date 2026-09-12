using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class SmallPortTests
{
    private const int TileCm = 200;
    private static readonly TileCoord Origin = new(2, 1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId StoneId = new(4);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void RepoDefs_SmallPort_MatchesChapterCostsAndHarbourBlueprint()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());
        var shop = Index(LoadShop());

        Assert.True(buildings.TryGetValue(SmallPort.BuildingId, out var port));
        Assert.True(recipes.TryGetValue(port.Recipe, out var recipe));
        Assert.Equal("recipe_small_port", port.Recipe);
        Assert.Equal(SmallPort.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(1200, port.Hp);
        Assert.Equal(SmallPort.FootprintW, port.Footprint.W);
        Assert.Equal(SmallPort.FootprintH, port.Footprint.H);
        Assert.Equal(4, port.Rotations);
        Assert.False(port.OnStreet);
        Assert.Equal(WaterPlacement.ShoreDeep, port.OnWater);
        Assert.Equal(15, port.MaxSlopeDeg);
        Assert.False(port.DragLine);
        Assert.Equal(BuildingBehaviour.Port, port.Behaviour);
        Assert.Null(port.Container);
        Assert.Equal(SmallPort.BlueprintId, recipe.Blueprint);
        Assert.Equal(3, recipe.UnlockShift);
        Assert.Equal("plank", recipe.Inputs[0].Item);
        Assert.Equal(16, recipe.Inputs[0].Count);
        Assert.Equal("stone", recipe.Inputs[1].Item);
        Assert.Equal(10, recipe.Inputs[1].Count);
        Assert.Equal("iron_ingot", recipe.Inputs[2].Item);
        Assert.Equal(6, recipe.Inputs[2].Count);
        Assert.Equal(2, SmallPort.DeepWaterTiles);

        Assert.True(shop.TryGetValue(SmallPort.BlueprintId, out var row));
        Assert.Equal(ShopKind.Blueprint, row.Kind);
        Assert.Equal(500, row.Price);
        Assert.Equal(3, row.FromShift);
        Assert.True(row.OncePerRun);
        Assert.Equal(SmallPort.BlueprintId, row.GrantBlueprint);

        Assert.False(buildings.ContainsKey("deep_water_port"));
        Assert.False(buildings.ContainsKey("deep_port"));
        Assert.DoesNotContain(shop.Keys, id => id.Contains("deep", StringComparison.Ordinal));
    }

    [Fact]
    public void Place_ShoreWithTwoDeepTiles_ConsumesRecipeAndOccupiesTwelve()
    {
        var fx = Loaded(planks: 16, stone: 10, iron: 6, field: ShorePad(Facing.East));
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(SmallPort.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(SmallPort.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.Equal(0, CountItem(fx, StoneId));
        Assert.Equal(0, CountItem(fx, IronId));
        var occupied = SmallPort.Occupied(Origin, Facing.East);
        Assert.Equal(12, occupied.Length);
        for (int i = 0; i < occupied.Length; i++)
        {
            Assert.True(fx.Registry.TryGetAt(occupied[i], out var at));
            Assert.Equal(placed.Construct.Id, at.Id);
        }
    }

    [Fact]
    public void Place_AllLand_RejectedAsWater()
    {
        var fx = Loaded(planks: 16, stone: 10, iron: 6);
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(SmallPort.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(PlaceReject.Water, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(16, CountItem(fx, PlankId));
    }

    [Fact]
    public void Place_AllDeep_RejectedAsWater()
    {
        var fx = Loaded(planks: 16, stone: 10, iron: 6, field: AllDeep());
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(SmallPort.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(PlaceReject.Water, rejected.Reason);
        Assert.Equal(16, CountItem(fx, PlankId));
    }

    [Fact]
    public void ParkingZoneAndLoadingFace_BerthTowardFacing()
    {
        var park = SmallPort.ParkingZone(Origin, Facing.East);
        var face = SmallPort.LoadingFace(Origin, Facing.East);
        var deep = SmallPort.DeepTiles(Origin, Facing.East);

        Assert.Equal(new TileCoord(4, 2), park);
        Assert.Equal(new TileCoord(1, 2), face);
        Assert.Equal(new[] { new TileCoord(4, 2), new TileCoord(5, 2) }, deep);
        Assert.Equal(park, deep[0]);
    }

    [Fact]
    public void HoldsParked_OnlyMotorboatOnBerth()
    {
        var inventory = new InventorySystem(new MaterialCatalog());
        var vehicles = new VehicleTable();
        var parked = vehicles.SpawnMotorboat(PlayerPose.FromMeters(9.0, 5.0, 0.0, 16384), inventory);
        Assert.Equal(new TileCoord(4, 2), parked.Tile(TileCm));
        Assert.True(SmallPort.HoldsParked(Origin, Facing.East, parked, TileCm));

        var rowboat = vehicles.SpawnRowboat(PlayerPose.FromMeters(9.0, 5.0, 0.0, 16384));
        Assert.False(SmallPort.HoldsParked(Origin, Facing.East, rowboat, TileCm));

        parked.SetKinematics(PlayerPose.FromMeters(5.0, 5.0, 0.0, 16384), 0f);
        Assert.False(SmallPort.HoldsParked(Origin, Facing.East, parked, TileCm));
    }

    private static PlacementField ShorePad(Facing facing)
    {
        var field = PlacementField.Flat(10, 8, TileCm);
        var deep = SmallPort.DeepTiles(Origin, facing);
        return field
            .WithHeight(deep[0], -200)
            .WithHeight(deep[1], -200);
    }

    private static PlacementField AllDeep()
    {
        var field = PlacementField.Flat(10, 8, TileCm);
        var occupied = SmallPort.Occupied(Origin, Facing.East);
        for (int i = 0; i < occupied.Length; i++)
            field = field.WithHeight(occupied[i], -200);
        return field;
    }

    private static int CountItem(Fixture fx, ItemDefId id)
    {
        Assert.True(fx.Inv.TryGetContainer(fx.Bag, out var grid));
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is ItemStack item && item.Item.Equals(id))
                n += item.Count;
        }

        return n;
    }

    private static Fixture Loaded(int planks = 0, int iron = 0, int stone = 0, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (planks > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, planks))));
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        if (stone > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(StoneId, stone))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(10, 8, TileCm),
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
                _ => throw new InvalidOperationException(def.GetType().Name)
            };
            map.Add(id, def);
        }

        return map;
    }

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["plank"] = PlankId,
        ["iron_ingot"] = IronId,
        ["stone"] = StoneId
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

    private sealed class MaterialCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.FootprintOf(key);
            return new Footprint(1, 1);
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.MaxStackOf(key);
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
