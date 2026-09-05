using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class BeltMk1Tests
{
    private static readonly TileCoord Origin = new(1, 1);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void RepoDefs_BeltMk1_MatchesChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(BeltNetwork.BuildingId, out var belt));
        Assert.True(recipes.TryGetValue(belt.Recipe, out var recipe));
        Assert.Equal("recipe_belt_mk1", belt.Recipe);
        Assert.Equal(BeltNetwork.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(80, belt.Hp);
        Assert.Equal(1, belt.Footprint.W);
        Assert.Equal(1, belt.Footprint.H);
        Assert.False(belt.OnStreet);
        Assert.Equal(WaterPlacement.None, belt.OnWater);
        Assert.Equal(15, belt.MaxSlopeDeg);
        Assert.Equal(BuildingBehaviour.Belt, belt.Behaviour);
        Assert.Null(belt.Container);
        Assert.Null(recipe.Blueprint);
        Assert.Equal("plank", recipe.Inputs[0].Item);
        Assert.Equal(1, recipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", recipe.Inputs[1].Item);
        Assert.Equal(1, recipe.Inputs[1].Count);
    }

    [Fact]
    public void Place_FourTiles_ConsumesOnePlankAndOneIngotEach()
    {
        var fx = Loaded(planks: 4, iron: 4);

        for (int x = 0; x < 4; x++)
            Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1 + x, 1), Facing.East, Owner));

        Assert.Equal(4, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.Equal(0, CountItem(fx, IronId));
    }

    [Fact]
    public void Street_Rejected_DoesNotConsume()
    {
        var field = PlacementField.Flat(8, 6, 200).WithStreet(Origin);
        var fx = Loaded(planks: 1, iron: 1, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    [Fact]
    public void Water_Rejected_DoesNotConsume()
    {
        var field = PlacementField.Flat(8, 6, 200).WithHeight(Origin, 0);
        var fx = Loaded(planks: 1, iron: 1, field);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        Assert.Equal(PlaceReject.Water, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    [Fact]
    public void Occupied_Rejected_DoesNotConsume()
    {
        var fx = Loaded(planks: 2, iron: 2);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.North));

        Assert.Equal(PlaceReject.Occupied, rejected.Reason);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
        Assert.Equal(1, CountItem(fx, IronId));
    }

    [Fact]
    public void MissingInput_Rejected()
    {
        var fx = Loaded(planks: 1, iron: 0);

        var rejected = Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(BeltNetwork.BuildingId, Origin, Facing.East));

        Assert.Equal(PlaceReject.MissingInput, rejected.Reason);
        Assert.Equal(0, fx.Registry.Count);
        Assert.Equal(1, CountItem(fx, PlankId));
    }

    [Fact]
    public void Compile_FourEastTiles_OneEightMetreSegment()
    {
        var fx = PlaceEastRun(4);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(Facing.East, segment.Facing);
        Assert.Equal(8f, segment.LengthMetres);
        Assert.Equal(4, segment.Tiles.Count);
        Assert.Equal(new TileCoord(1, 1), segment.Tiles[0]);
        Assert.Equal(new TileCoord(4, 1), segment.Tiles[3]);
        Assert.Equal(BeltNetwork.LaneCount, 2);
        Assert.Empty(segment.Lane(0));
        Assert.Empty(segment.Lane(1));
    }

    [Fact]
    public void Compile_TwoPlusAdjacentSameFacing_IsOneSegmentNotPerTile()
    {
        var fx = PlaceEastRun(2);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);

        var segment = Assert.Single(belts.Segments);
        Assert.Equal(4f, segment.LengthMetres);
        Assert.Equal(2, segment.Tiles.Count);
    }

    [Fact]
    public void Compile_ThreeTileLine_DifferentLengthAndHash()
    {
        var four = CompileEast(4);
        var three = CompileEast(3);

        Assert.Equal(8f, four.LengthMetres);
        Assert.Equal(6f, three.LengthMetres);
        Assert.NotEqual(four.RunHash, three.RunHash);
    }

    [Fact]
    public void Compile_SameRunTwice_HashesMatch()
    {
        var fx = PlaceEastRun(4);
        var first = new BeltNetwork();
        first.Compile(fx.Registry.All);
        var second = new BeltNetwork();
        second.Compile(fx.Registry.All);

        Assert.Equal(first.Segments[0].RunHash, second.Segments[0].RunHash);
        Assert.Equal(first.Segments[0].LengthMetres, second.Segments[0].LengthMetres);
        Assert.Equal(first.Segments[0].Tiles, second.Segments[0].Tiles);
    }

    [Fact]
    public void Compile_GapOrOppositeFacing_IsTwoSegments()
    {
        var gap = Loaded(planks: 2, iron: 2);
        Assert.IsType<Placed>(gap.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East));
        Assert.IsType<Placed>(gap.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(3, 1), Facing.East));
        var gapBelts = new BeltNetwork();
        gapBelts.Compile(gap.Registry.All);
        Assert.Equal(2, gapBelts.Segments.Count);

        var opposed = Loaded(planks: 2, iron: 2);
        Assert.IsType<Placed>(opposed.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East));
        Assert.IsType<Placed>(opposed.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(2, 1), Facing.West));
        var opposedBelts = new BeltNetwork();
        opposedBelts.Compile(opposed.Registry.All);
        Assert.Equal(2, opposedBelts.Segments.Count);
    }

    [Fact]
    public void Step_Lane0_ThirtyTicks_AtTwoMetres_Lane1Empty()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 11, 0f));

        belts.StepTicks(TickClock.TickHz);

        var item = Assert.Single(segment.Lane(0));
        Assert.Equal(11, item.ItemId);
        Assert.Equal(ExpectedMetres(0f, TickClock.TickHz), item.MetresFromStart, 3);
        Assert.Equal(2f, item.MetresFromStart, 3);
        Assert.Empty(segment.Lane(1));
    }

    [Fact]
    public void Step_Lane1_ThirtyTicks_AtTwoMetres_Lane0Empty()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(1, 22, 0f));

        belts.StepTicks(TickClock.TickHz);

        var item = Assert.Single(segment.Lane(1));
        Assert.Equal(22, item.ItemId);
        Assert.Equal(ExpectedMetres(0f, TickClock.TickHz), item.MetresFromStart, 3);
        Assert.Equal(2f, item.MetresFromStart, 3);
        Assert.Empty(segment.Lane(0));
    }

    [Fact]
    public void Step_BothLanes_MoveIndependently()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 1, 0f));
        Assert.True(segment.TryInsert(1, 2, 1f));

        belts.StepTicks(TickClock.TickHz);

        Assert.Equal(ExpectedMetres(0f, TickClock.TickHz), Assert.Single(segment.Lane(0)).MetresFromStart, 3);
        Assert.Equal(ExpectedMetres(1f, TickClock.TickHz), Assert.Single(segment.Lane(1)).MetresFromStart, 3);
    }

    [Fact]
    public void Insert_CloserThanHalfMetreBehindHead_Rejected()
    {
        var segment = CompileEast(4);
        Assert.True(segment.TryInsert(0, 1, 0f));
        Assert.False(segment.TryInsert(0, 2, 0.4f));
        Assert.True(segment.TryInsert(0, 3, 0.5f));
        Assert.Equal(2, segment.Lane(0).Count);
    }

    [Fact]
    public void Step_BlockedHead_DoesNotOverlap()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 1, segment.LengthMetres));
        Assert.True(segment.TryInsert(0, 2, 7.2f));

        belts.StepTicks(TickClock.TickHz);

        Assert.Equal(8f, segment.Lane(0)[0].MetresFromStart, 3);
        float follower = segment.Lane(0)[1].MetresFromStart;
        float headGap = segment.Lane(0)[0].MetresFromStart - BeltNetwork.MinSpacingMetres;
        Assert.Equal(Math.Min(ExpectedMetres(7.2f, TickClock.TickHz), headGap), follower, 3);
        Assert.True(segment.Lane(0)[0].MetresFromStart - follower >= BeltNetwork.MinSpacingMetres);
    }

    [Fact]
    public void Step_BlockedHead_BothLanesJamWithoutOverlap()
    {
        var belts = CompileEastNetwork(4);
        var segment = Assert.Single(belts.Segments);
        Assert.True(segment.TryInsert(0, 1, 8f));
        Assert.True(segment.TryInsert(0, 2, 7.2f));
        Assert.True(segment.TryInsert(1, 3, 8f));
        Assert.True(segment.TryInsert(1, 4, 7.2f));

        belts.StepTicks(60);

        for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
        {
            Assert.Equal(8f, segment.Lane(lane)[0].MetresFromStart, 3);
            Assert.Equal(7.5f, segment.Lane(lane)[1].MetresFromStart, 3);
        }
    }

    private static BeltNetwork CompileEastNetwork(int tiles)
    {
        var fx = PlaceEastRun(tiles);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        return belts;
    }

    private static BeltSegment CompileEast(int tiles) =>
        Assert.Single(CompileEastNetwork(tiles).Segments);

    private static Fixture PlaceEastRun(int tiles)
    {
        var fx = Loaded(planks: tiles, iron: tiles);
        for (int i = 0; i < tiles; i++)
            Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1 + i, 1), Facing.East, Owner));
        return fx;
    }

    private static float ExpectedMetres(float start, int ticks)
    {
        float t = ticks / (float)TickClock.TickHz;
        return start + BeltNetwork.Mk1MetresPerSecond * t;
    }

    private static Fixture Loaded(int planks, int iron, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (planks > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, planks))));
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
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
        ["log"] = LogId,
        ["plank"] = PlankId,
        ["iron_ingot"] = IronId
    };

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
            if (key.Def == LogId.Value) return new Footprint(1, 2);
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return new Footprint(1, 1);
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            if (key.Def == LogId.Value) return 10;
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return 20;
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
