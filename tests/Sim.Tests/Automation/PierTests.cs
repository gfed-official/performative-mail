using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class PierTests
{
    private static readonly TileCoord Origin = new(4, 4);
    private static readonly ItemDefId LogId = new(1);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void RepoDefs_Pier_MatchesChapterCostsAndShallowWater()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(Pier.BuildingId, out var pier));
        Assert.True(recipes.TryGetValue(pier.Recipe, out var recipe));
        Assert.Equal("recipe_pier", pier.Recipe);
        Assert.Equal(Pier.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(300, pier.Hp);
        Assert.Equal(1, pier.Footprint.W);
        Assert.Equal(Pier.FootprintLength, pier.Footprint.H);
        Assert.Equal(4, pier.Rotations);
        Assert.False(pier.OnStreet);
        Assert.Equal(WaterPlacement.Shallow, pier.OnWater);
        Assert.Equal(15, pier.MaxSlopeDeg);
        Assert.False(pier.DragLine);
        Assert.Equal(BuildingBehaviour.Pier, pier.Behaviour);
        Assert.Null(pier.Container);
        Assert.Null(recipe.Blueprint);
        Assert.Equal(1, recipe.UnlockShift);
        Assert.Equal("log", recipe.Inputs[0].Item);
        Assert.Equal(6, recipe.Inputs[0].Count);
    }

    [Fact]
    public void Place_ShallowWater_ConsumesLogsAndOccupiesThreeTiles()
    {
        var fx = Loaded(logs: 6, field: ShallowPad());
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(Pier.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(Pier.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, LogId));
        Assert.True(fx.Registry.TryGetAt(Origin, out var at));
        Assert.Equal(placed.Construct.Id, at.Id);
        Assert.True(fx.Registry.TryGetAt(new TileCoord(5, 4), out var mid));
        Assert.Equal(placed.Construct.Id, mid.Id);
        Assert.True(fx.Registry.TryGetAt(new TileCoord(6, 4), out var end));
        Assert.Equal(placed.Construct.Id, end.Id);
        Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(Pier.BuildingId, Origin, Facing.East, Owner));
    }

    [Fact]
    public void Place_DryLand_Rejected()
    {
        var fx = Loaded(logs: 6);
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(Pier.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(PlaceReject.DryLand, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(6, CountItem(fx.Inv, fx.Bag, LogId));
    }

    [Fact]
    public void Preview_DryLand_DoesNotConsume()
    {
        var fx = Loaded(logs: 6);

        Assert.Equal(PlaceReject.DryLand, fx.Registry.Preview(Pier.BuildingId, Origin, Facing.East));
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(6, CountItem(fx.Inv, fx.Bag, LogId));
    }

    [Fact]
    public void Place_DeepWater_RejectedAsDryLand()
    {
        var fx = Loaded(logs: 6, field: DeepPad());
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(Pier.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(PlaceReject.DryLand, rejected.Reason);
        Assert.Equal(6, CountItem(fx.Inv, fx.Bag, LogId));
    }

    private static PlacementField ShallowPad()
    {
        var field = PlacementField.Flat(8, 6, 200);
        return field
            .WithHeight(Origin, -50)
            .WithHeight(new TileCoord(5, 4), -50)
            .WithHeight(new TileCoord(6, 4), -50);
    }

    private static PlacementField DeepPad()
    {
        var field = PlacementField.Flat(8, 6, 200);
        return field
            .WithHeight(Origin, -200)
            .WithHeight(new TileCoord(5, 4), -200)
            .WithHeight(new TileCoord(6, 4), -200);
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

    private static Fixture Loaded(int logs = 0, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (logs > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, logs))));
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

    private static Dictionary<string, T> Index<T>(T[] defs) where T : class
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            string id = def switch
            {
                BuildingDef building => building.Id,
                RecipeDef recipe => recipe.Id,
                _ => throw new InvalidOperationException(def.GetType().Name)
            };
            map.Add(id, def);
        }

        return map;
    }

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["log"] = LogId
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
            return key.Def == LogId.Value ? new Footprint(1, 2) : new Footprint(1, 1);
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
