using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Tests.World;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Building;

public sealed class ConstructRegistryTests
{
    private static readonly TileCoord Origin = new(1, 1);
    private static readonly ItemDefId LogId = new(1);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void Wall_ThreeLog_ConsumesAndRegisters()
    {
        var fx = Loaded(logs: 3);

        var placed = Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North, Owner));

        Assert.Equal("wall_wood", placed.Construct.DefId);
        Assert.Equal(Origin, placed.Construct.Tile);
        Assert.Equal(Facing.North, placed.Construct.Rotation);
        Assert.Equal(Owner, placed.Construct.Owner);
        Assert.Equal(300, placed.Construct.Hp);
        Assert.Equal(300, placed.Construct.MaxHp);
        Assert.Equal(EntityClass.Construct, placed.Construct.Id.Class);
        Assert.Equal(50331649u, placed.Construct.Id.Value);
        Assert.True(fx.Registry.TryGet(placed.Construct.Id, out var row));
        Assert.Equal(placed.Construct, row);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountLog(fx));
    }

    [Fact]
    public void Chest_FourLog_ConsumesAndRegisters()
    {
        var fx = Loaded(logs: 4);

        var placed = Assert.IsType<Placed>(fx.Registry.TryPlace("chest", Origin, Facing.East, Owner));

        Assert.Equal("chest", placed.Construct.DefId);
        Assert.Equal(200, placed.Construct.Hp);
        Assert.Equal(Facing.East, placed.Construct.Rotation);
        Assert.True(fx.Registry.TryGet(placed.Construct.Id, out _));
        Assert.Equal(0, CountLog(fx));
    }

    [Fact]
    public void Street_Rejected_DoesNotConsume()
    {
        var field = PlacementField.Flat(3, 3, 200).WithStreet(Origin);
        var fx = Loaded(logs: 3, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));

        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(3, CountLog(fx));
    }

    [Fact]
    public void Slope_AtOrAbove15Deg_Rejected()
    {
        var field = PlacementField.Flat(3, 3, 200, 100).WithHeight(new TileCoord(2, 1), 154);
        var fx = Loaded(logs: 3, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));

        Assert.Equal(PlaceReject.Slope, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(3, CountLog(fx));
    }

    [Fact]
    public void Slope_JustUnder15Deg_Places()
    {
        var field = PlacementField.Flat(3, 3, 200, 100).WithHeight(new TileCoord(2, 1), 153);
        var fx = Loaded(logs: 3, field);

        Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));
        Assert.Equal(0, CountLog(fx));
    }

    [Fact]
    public void MissingLog_Rejected()
    {
        var fx = Loaded(logs: 2);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));

        Assert.Equal(PlaceReject.MissingInput, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(2, CountLog(fx));
    }

    [Fact]
    public void Occupied_Rejected()
    {
        var fx = Loaded(logs: 7);
        Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace("chest", Origin, Facing.North));

        Assert.Equal(PlaceReject.Occupied, rejected.Reason);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(4, CountLog(fx));
    }

    [Fact]
    public void GenerateSmallIsland_StreetTile_Rejected()
    {
        var tables = WorldGen.GenerateSmallIsland(WorldGenHashTests.FixedSeed);
        var street = FirstStreetTile(tables);
        var field = PlacementField.FromWorld(tables);
        var fx = Loaded(logs: 3, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace("wall_wood", street, Facing.North));

        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(3, CountLog(fx));
    }

    [Fact]
    public void HarvestThenPlace_Wall_ConsumesAxeLogs()
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        var harvest = new HarvestSession(
            new[] { new ResourceNodeRecord(ResourceKind.Wood, new TileCoord(0, 0)) },
            inv,
            bag,
            Ids());
        Assert.IsType<Harvested>(harvest.Hit(new TileCoord(0, 0), HarvestTool.Axe));
        Assert.IsType<Harvested>(harvest.Hit(new TileCoord(0, 0), HarvestTool.Axe));
        Assert.Equal(4, CountItem(inv, bag, LogId));

        var registry = new ConstructRegistry(LoadBuildings(), LoadRecipes(), PlacementField.Flat(3, 3, 200), inv, bag, Ids());
        var placed = Assert.IsType<Placed>(registry.TryPlace("wall_wood", Origin, Facing.North));

        Assert.True(registry.TryGet(placed.Construct.Id, out _));
        Assert.Equal(1, CountItem(inv, bag, LogId));
    }

    [Fact]
    public void RepoDefs_WallChestAndBeltRecipes_MatchChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue("wall_wood", out var wall));
        Assert.True(recipes.TryGetValue(wall.Recipe, out var wallRecipe));
        Assert.Equal("wall_wood", wallRecipe.ProducesBuilding);
        Assert.Equal("log", wallRecipe.Inputs[0].Item);
        Assert.Equal(3, wallRecipe.Inputs[0].Count);
        Assert.Equal(300, wall.Hp);
        Assert.False(wall.OnStreet);
        Assert.Equal(15, wall.MaxSlopeDeg);

        Assert.True(buildings.TryGetValue("chest", out var chest));
        Assert.True(recipes.TryGetValue(chest.Recipe, out var chestRecipe));
        Assert.Equal("chest", chestRecipe.ProducesBuilding);
        Assert.Equal("log", chestRecipe.Inputs[0].Item);
        Assert.Equal(4, chestRecipe.Inputs[0].Count);
        Assert.Equal(200, chest.Hp);

        Assert.True(buildings.TryGetValue("belt_mk1", out var belt));
        Assert.True(recipes.TryGetValue(belt.Recipe, out var beltRecipe));
        Assert.Equal("belt_mk1", beltRecipe.ProducesBuilding);
        Assert.Equal("plank", beltRecipe.Inputs[0].Item);
        Assert.Equal(1, beltRecipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", beltRecipe.Inputs[1].Item);
        Assert.Equal(1, beltRecipe.Inputs[1].Count);
        Assert.Equal(80, belt.Hp);
        Assert.False(belt.OnStreet);
        Assert.Equal(BuildingBehaviour.Belt, belt.Behaviour);

        Assert.True(buildings.TryGetValue("belt_mk1_ramp", out var ramp));
        Assert.True(recipes.TryGetValue(ramp.Recipe, out var rampRecipe));
        Assert.Equal("belt_mk1_ramp", rampRecipe.ProducesBuilding);
        Assert.Equal("plank", rampRecipe.Inputs[0].Item);
        Assert.Equal(3, rampRecipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", rampRecipe.Inputs[1].Item);
        Assert.Equal(2, rampRecipe.Inputs[1].Count);
        Assert.Equal(120, ramp.Hp);
        Assert.Equal(2, ramp.Footprint.W);
        Assert.Equal(1, ramp.Footprint.H);
        Assert.False(ramp.OnStreet);
        Assert.Equal(BuildingBehaviour.Belt, ramp.Behaviour);

        Assert.True(buildings.TryGetValue("belt_mk1_elevated", out var elevated));
        Assert.True(recipes.TryGetValue(elevated.Recipe, out var elevatedRecipe));
        Assert.Equal("belt_mk1_elevated", elevatedRecipe.ProducesBuilding);
        Assert.Equal("plank", elevatedRecipe.Inputs[0].Item);
        Assert.Equal(2, elevatedRecipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", elevatedRecipe.Inputs[1].Item);
        Assert.Equal(1, elevatedRecipe.Inputs[1].Count);
        Assert.Equal(80, elevated.Hp);
        Assert.Equal(1, elevated.Footprint.W);
        Assert.Equal(1, elevated.Footprint.H);
        Assert.True(elevated.OnStreet);
        Assert.Equal(BuildingBehaviour.Belt, elevated.Behaviour);
    }

    [Fact]
    public void RepoDefs_SplitterAndMerger_MatchChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue("splitter", out var splitter));
        Assert.True(recipes.TryGetValue(splitter.Recipe, out var splitterRecipe));
        Assert.Equal("recipe_splitter", splitter.Recipe);
        Assert.Equal("splitter", splitterRecipe.ProducesBuilding);
        Assert.Equal(150, splitter.Hp);
        Assert.Equal(1, splitter.Footprint.W);
        Assert.Equal(1, splitter.Footprint.H);
        Assert.False(splitter.OnStreet);
        Assert.Equal(WaterPlacement.None, splitter.OnWater);
        Assert.Equal(15, splitter.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Splitter, splitter.Behaviour);
        Assert.Null(splitterRecipe.Blueprint);
        Assert.Equal("iron_ingot", splitterRecipe.Inputs[0].Item);
        Assert.Equal(2, splitterRecipe.Inputs[0].Count);
        Assert.Equal("plank", splitterRecipe.Inputs[1].Item);
        Assert.Equal(1, splitterRecipe.Inputs[1].Count);

        Assert.True(buildings.TryGetValue("merger", out var merger));
        Assert.True(recipes.TryGetValue(merger.Recipe, out var mergerRecipe));
        Assert.Equal("recipe_merger", merger.Recipe);
        Assert.Equal("merger", mergerRecipe.ProducesBuilding);
        Assert.Equal(150, merger.Hp);
        Assert.Equal(1, merger.Footprint.W);
        Assert.Equal(1, merger.Footprint.H);
        Assert.False(merger.OnStreet);
        Assert.Equal(WaterPlacement.None, merger.OnWater);
        Assert.Equal(15, merger.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Merger, merger.Behaviour);
        Assert.Null(mergerRecipe.Blueprint);
        Assert.Equal("iron_ingot", mergerRecipe.Inputs[0].Item);
        Assert.Equal(2, mergerRecipe.Inputs[0].Count);
        Assert.Equal("plank", mergerRecipe.Inputs[1].Item);
        Assert.Equal(1, mergerRecipe.Inputs[1].Count);
    }

    private static Fixture Loaded(int logs, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (logs > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, logs))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(3, 3, 200),
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

    private static int CountLog(Fixture fx) => CountItem(fx.Inv, fx.Bag, LogId);

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

    private static TileCoord FirstStreetTile(WorldTables tables)
    {
        foreach (var street in tables.Streets)
        {
            if (street.Tiles is { Length: > 0 })
                return street.Tiles[0];
        }

        throw new InvalidOperationException("Generated island has no street tiles.");
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

    private readonly record struct Fixture(ConstructRegistry Registry, InventorySystem Inv, ContainerId Bag);

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
