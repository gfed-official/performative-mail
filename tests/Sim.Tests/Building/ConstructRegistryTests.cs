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
    private static readonly TileCoord LineEnd = new(6, 1);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
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
        Assert.True(wall.DragLine);
        Assert.True(belt.DragLine);
        Assert.False(chest.DragLine);

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

    [Fact]
    public void PlaceLine_SixWalls_OneRequestConsumesEighteenLogs()
    {
        var fx = Loaded(logs: 18, PlacementField.Flat(8, 3, 200));

        var line = Assert.IsType<PlaceLineApplied>(
            fx.Registry.TryPlaceLine("wall_wood", Origin, LineEnd, Facing.North, Owner));

        Assert.Equal(6, line.Tiles.Count);
        for (int i = 0; i < line.Tiles.Count; i++)
        {
            Assert.Equal(new TileCoord(1 + i, 1), line.Tiles[i].Tile);
            var placed = Assert.IsType<Placed>(line.Tiles[i].Result);
            Assert.Equal("wall_wood", placed.Construct.DefId);
            Assert.Equal(new TileCoord(1 + i, 1), placed.Construct.Tile);
        }

        Assert.Equal(6, fx.Registry.Count);
        Assert.Equal(0, CountLog(fx));
    }

    [Fact]
    public void PlaceLine_SixBelts_OneRequestConsumesSixEach()
    {
        var fx = Loaded(logs: 0, PlacementField.Flat(8, 3, 200), planks: 6, iron: 6);

        var line = Assert.IsType<PlaceLineApplied>(
            fx.Registry.TryPlaceLine("belt_mk1", Origin, LineEnd, Facing.East, Owner));

        Assert.Equal(6, line.Tiles.Count);
        for (int i = 0; i < line.Tiles.Count; i++)
            Assert.IsType<Placed>(line.Tiles[i].Result);

        Assert.Equal(6, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, PlankId));
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, IronId));
    }

    [Fact]
    public void PlaceLine_MidOccupied_FivePlaceOneReject()
    {
        var fx = Loaded(logs: 18, PlacementField.Flat(8, 3, 200));
        Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", new TileCoord(3, 1), Facing.North));

        var line = Assert.IsType<PlaceLineApplied>(
            fx.Registry.TryPlaceLine("wall_wood", Origin, LineEnd, Facing.North, Owner));

        Assert.Equal(6, line.Tiles.Count);
        int placed = 0;
        int rejected = 0;
        for (int i = 0; i < line.Tiles.Count; i++)
        {
            if (line.Tiles[i].Result is Placed)
            {
                placed++;
                continue;
            }

            var fail = Assert.IsType<PlaceRejected>(line.Tiles[i].Result);
            Assert.Equal(PlaceReject.Occupied, fail.Reason);
            Assert.Equal(new TileCoord(3, 1), line.Tiles[i].Tile);
            rejected++;
        }

        Assert.Equal(5, placed);
        Assert.Equal(1, rejected);
        Assert.Equal(6, fx.Registry.Count);
        Assert.Equal(0, CountLog(fx));
    }

    [Fact]
    public void PlaceLine_MaterialsForThreeOfSix_ThreeMissingInput()
    {
        var fx = Loaded(logs: 9, PlacementField.Flat(8, 3, 200));

        var line = Assert.IsType<PlaceLineApplied>(
            fx.Registry.TryPlaceLine("wall_wood", Origin, LineEnd, Facing.North, Owner));

        Assert.Equal(6, line.Tiles.Count);
        for (int i = 0; i < 3; i++)
            Assert.IsType<Placed>(line.Tiles[i].Result);
        for (int i = 3; i < 6; i++)
        {
            var rejected = Assert.IsType<PlaceRejected>(line.Tiles[i].Result);
            Assert.Equal(PlaceReject.MissingInput, rejected.Reason);
        }

        Assert.Equal(3, fx.Registry.Count);
        Assert.Equal(0, CountLog(fx));
    }

    [Fact]
    public void PlaceLine_Diagonal_RejectedNoConsume()
    {
        var fx = Loaded(logs: 18, PlacementField.Flat(8, 3, 200));

        var rejected = Assert.IsType<PlaceLineRejected>(
            fx.Registry.TryPlaceLine("wall_wood", Origin, new TileCoord(3, 2), Facing.North));

        Assert.Equal(PlaceLineReject.NotStraight, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(18, CountLog(fx));
    }

    [Fact]
    public void PlaceLine_Chest_RejectedNoConsume()
    {
        var fx = Loaded(logs: 8, PlacementField.Flat(8, 3, 200));

        var rejected = Assert.IsType<PlaceLineRejected>(
            fx.Registry.TryPlaceLine("chest", Origin, LineEnd, Facing.North));

        Assert.Equal(PlaceLineReject.NotDragLine, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(8, CountLog(fx));
    }

    [Fact]
    public void Deconstruct_WallPrep_RefundsThreeLogs()
    {
        var fx = Loaded(logs: 3);
        var placed = Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));

        var gone = Assert.IsType<Deconstructed>(fx.Registry.TryDeconstruct(placed.Construct.Id, 1.0));

        Assert.Equal(placed.Construct, gone.Construct);
        Assert.Equal(0, fx.Registry.Count);
        Assert.False(fx.Registry.TryGet(placed.Construct.Id, out _));
        Assert.Equal(3, CountLog(fx));
        Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));
    }

    [Fact]
    public void Deconstruct_WallDelivery_RefundsOneLog()
    {
        var fx = Loaded(logs: 3);
        var placed = Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Origin, Facing.North));

        Assert.IsType<Deconstructed>(fx.Registry.TryDeconstruct(placed.Construct.Id, 0.5));

        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountLog(fx));
    }

    [Fact]
    public void Deconstruct_ChestDelivery_RefundsTwoLogs()
    {
        var fx = Loaded(logs: 4);
        var placed = Assert.IsType<Placed>(fx.Registry.TryPlace("chest", Origin, Facing.East));

        Assert.IsType<Deconstructed>(fx.Registry.TryDeconstruct(placed.Construct.Id, 0.5));

        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(2, CountLog(fx));
    }

    [Fact]
    public void PlaceLine_SixWalls_PrepRefundsEighteen_DeliveryRefundsSix()
    {
        var prep = Loaded(logs: 18, PlacementField.Flat(8, 3, 200));
        var prepLine = Assert.IsType<PlaceLineApplied>(
            prep.Registry.TryPlaceLine("wall_wood", Origin, LineEnd, Facing.North, Owner));
        for (int i = 0; i < prepLine.Tiles.Count; i++)
        {
            var placed = Assert.IsType<Placed>(prepLine.Tiles[i].Result);
            Assert.IsType<Deconstructed>(prep.Registry.TryDeconstruct(placed.Construct.Id, 1.0));
        }

        Assert.Equal(0, prep.Registry.Count);
        Assert.Equal(18, CountLog(prep));

        var delivery = Loaded(logs: 18, PlacementField.Flat(8, 3, 200));
        var deliveryLine = Assert.IsType<PlaceLineApplied>(
            delivery.Registry.TryPlaceLine("wall_wood", Origin, LineEnd, Facing.North, Owner));
        for (int i = 0; i < deliveryLine.Tiles.Count; i++)
        {
            var placed = Assert.IsType<Placed>(deliveryLine.Tiles[i].Result);
            Assert.IsType<Deconstructed>(delivery.Registry.TryDeconstruct(placed.Construct.Id, 0.5));
        }

        Assert.Equal(0, delivery.Registry.Count);
        Assert.Equal(6, CountLog(delivery));
    }

    [Fact]
    public void Deconstruct_BeltPrep_RefundsBothInputs()
    {
        var fx = Loaded(logs: 0, planks: 1, iron: 1);
        var placed = Assert.IsType<Placed>(fx.Registry.TryPlace("belt_mk1", Origin, Facing.East));

        Assert.IsType<Deconstructed>(fx.Registry.TryDeconstruct(placed.Construct.Id, 1.0));

        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx.Inv, fx.Bag, PlankId));
        Assert.Equal(1, CountItem(fx.Inv, fx.Bag, IronId));
    }

    [Fact]
    public void Deconstruct_SecondRefundNoRoom_KeepsConstructAndTakesBack()
    {
        var catalog = new MaterialCatalog(otherMax: 1);
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(new ContainerSpec(ContainerShape.Grid(2, 1), null));
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, 1))));
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, 1))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            PlacementField.Flat(3, 3, 200),
            inv,
            bag,
            Ids());
        var placed = Assert.IsType<Placed>(registry.TryPlace("belt_mk1", Origin, Facing.East));
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, 1))));

        var rejected = Assert.IsType<DeconstructRejected>(registry.TryDeconstruct(placed.Construct.Id, 1.0));

        Assert.Equal(DeconstructReject.NoRoom, rejected.Reason);
        Assert.Equal(1, registry.Count);
        Assert.True(registry.TryGet(placed.Construct.Id, out _));
        Assert.Equal(1, CountItem(inv, bag, PlankId));
        Assert.Equal(0, CountItem(inv, bag, IronId));
    }

    [Fact]
    public void Deconstruct_Unknown_Rejected()
    {
        var fx = Loaded(logs: 3);
        var missing = EntityId.FromClassAndCounter(EntityClass.Construct, 99);

        var rejected = Assert.IsType<DeconstructRejected>(fx.Registry.TryDeconstruct(missing, 1.0));

        Assert.Equal(DeconstructReject.UnknownConstruct, rejected.Reason);
        Assert.Equal(3, CountLog(fx));
    }

    private static Fixture Loaded(int logs, PlacementField? field = null, int planks = 0, int iron = 0)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        DepositChunks(inv, bag, LogId, logs, 10);
        DepositChunks(inv, bag, PlankId, planks, 20);
        DepositChunks(inv, bag, IronId, iron, 20);
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
        ["log"] = LogId,
        ["plank"] = PlankId,
        ["iron_ingot"] = IronId
    };

    private static void DepositChunks(
        InventorySystem inv,
        ContainerId bag,
        ItemDefId id,
        int count,
        int maxStack)
    {
        int left = count;
        while (left > 0)
        {
            int n = left < maxStack ? left : maxStack;
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(id, n))));
            left -= n;
        }
    }

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
        private readonly int _logMax;
        private readonly int _otherMax;

        public MaterialCatalog(int logMax = 10, int otherMax = 20)
        {
            _logMax = logMax;
            _otherMax = otherMax;
        }

        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            if (key.Def == LogId.Value) return new Footprint(1, 2);
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return new Footprint(1, 1);
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            if (key.Def == LogId.Value) return _logMax;
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return _otherMax;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return WeightClass.Light;
        }

        public StackCategory CategoryOf(StackKey key) => StackCategory.Material;
    }
}
