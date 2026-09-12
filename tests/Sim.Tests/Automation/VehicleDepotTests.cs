using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class VehicleDepotTests
{
    private const int TileCm = 200;
    private static readonly TileCoord Origin = new(5, 5);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId StoneId = new(4);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly AddressId Oak = new(1, 4, 13, 0);

    [Fact]
    public void RepoDefs_VehicleDepot_MatchesChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(VehicleDepot.BuildingId, out var depot));
        Assert.True(recipes.TryGetValue(depot.Recipe, out var recipe));
        Assert.Equal("recipe_vehicle_depot", depot.Recipe);
        Assert.Equal(VehicleDepot.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(1000, depot.Hp);
        Assert.Equal(3, depot.Footprint.W);
        Assert.Equal(3, depot.Footprint.H);
        Assert.Equal(4, depot.Rotations);
        Assert.False(depot.OnStreet);
        Assert.Equal(WaterPlacement.None, depot.OnWater);
        Assert.Equal(15, depot.MaxSlopeDeg);
        Assert.False(depot.DragLine);
        Assert.Equal(BuildingBehaviour.VehicleDepot, depot.Behaviour);
        Assert.Null(depot.Container);
        Assert.Equal("bp_truck", recipe.Blueprint);
        Assert.Equal(2, recipe.UnlockShift);
        Assert.Equal("plank", recipe.Inputs[0].Item);
        Assert.Equal(12, recipe.Inputs[0].Count);
        Assert.Equal("stone", recipe.Inputs[1].Item);
        Assert.Equal(8, recipe.Inputs[1].Count);
        Assert.Equal("iron_ingot", recipe.Inputs[2].Item);
        Assert.Equal(4, recipe.Inputs[2].Count);
        Assert.Equal(3, VehicleDepot.FootprintTiles);
    }

    [Fact]
    public void Place_ConsumesRecipe_AndOccupiesNineTiles()
    {
        var fx = Loaded(planks: 12, stone: 8, iron: 4);
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(VehicleDepot.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(VehicleDepot.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.Equal(0, CountItem(fx, StoneId));
        Assert.Equal(0, CountItem(fx, IronId));
        var occupied = VehicleDepot.Occupied(Origin);
        Assert.Equal(9, occupied.Length);
        for (int i = 0; i < occupied.Length; i++)
        {
            Assert.True(fx.Registry.TryGetAt(occupied[i], out var at));
            Assert.Equal(placed.Construct.Id, at.Id);
        }

        Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(VehicleDepot.BuildingId, Origin, Facing.East, Owner));
        Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(VehicleDepot.BuildingId, new TileCoord(6, 6), Facing.North, Owner));
    }

    [Fact]
    public void Place_StreetTile_Rejects()
    {
        var fx = Loaded(
            planks: 12,
            stone: 8,
            iron: 4,
            field: PlacementField.Flat(14, 12, TileCm).WithStreet(Origin));
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(VehicleDepot.BuildingId, Origin, Facing.East, Owner));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(12, CountItem(fx, PlankId));
    }

    [Theory]
    [InlineData(Facing.East, 7, 6)]
    [InlineData(Facing.North, 6, 7)]
    [InlineData(Facing.South, 6, 5)]
    [InlineData(Facing.West, 5, 6)]
    public void ParkingZoneAndLoadingFace_OneEach_FollowFacing(Facing facing, int faceX, int faceY)
    {
        var park = VehicleDepot.ParkingZone(Origin);
        var face = VehicleDepot.LoadingFace(Origin, facing);
        Assert.Equal(new TileCoord(6, 6), park);
        Assert.Equal(new TileCoord(faceX, faceY), face);
        Assert.NotEqual(park, face);

        int parks = 0;
        int faces = 0;
        var occupied = VehicleDepot.Occupied(Origin);
        for (int i = 0; i < occupied.Length; i++)
        {
            if (occupied[i].Equals(park)) parks++;
            if (occupied[i].Equals(face)) faces++;
        }

        Assert.Equal(1, parks);
        Assert.Equal(1, faces);
    }

    [Fact]
    public void HoldsParked_OnlyWhenTruckStoppedOnParkingZone()
    {
        var inventory = new InventorySystem(new MaterialCatalog());
        var vehicles = new VehicleTable();
        var parked = vehicles.SpawnMailTruck(PlayerPose.FromMeters(13.0, 13.0, 0.0, 16384), inventory);
        Assert.Equal(new TileCoord(6, 6), parked.Tile(TileCm));
        Assert.True(parked.IsParked);
        Assert.True(VehicleDepot.HoldsParked(Origin, parked, TileCm));

        var elsewhere = vehicles.SpawnMailTruck(PlayerPose.FromMeters(9.0, 13.0, 0.0, 16384), inventory);
        Assert.False(VehicleDepot.HoldsParked(Origin, elsewhere, TileCm));

        parked.Apply(
            new InputCmd(0, 0, MovementStep.AxisFull, 16384, InputButtons.None),
            VehicleContext.MailTruckOnRoad);
        Assert.False(parked.IsParked);
        Assert.False(VehicleDepot.HoldsParked(Origin, parked, TileCm));
    }

    [Fact]
    public void Site_OneRoute_PreservesStopOrder_AndRoundTrips()
    {
        var fx = Loaded(planks: 12, stone: 8, iron: 4);
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(VehicleDepot.BuildingId, Origin, Facing.East, Owner));
        var site = new VehicleDepotSite(placed.Construct.Id, Origin, Facing.East);
        Assert.Equal(VehicleDepot.ParkingZone(Origin), site.ParkingZone);
        Assert.Equal(VehicleDepot.LoadingFace(Origin, Facing.East), site.LoadingFace);
        Assert.Empty(site.Route.Stops);

        var port = EntityId.FromClassAndCounter(EntityClass.Construct, 9);
        var first = new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForDistrict(2),
            RouteStop.ForConstruct(port)
        };
        site.Route.ReplaceStops(first);
        Assert.Equal(first, site.Route.Stops);

        var reversed = new[]
        {
            RouteStop.ForConstruct(port),
            RouteStop.ForDistrict(2),
            RouteStop.ForAddress(Oak)
        };
        site.Route.ReplaceStops(reversed);
        Assert.Equal(reversed, site.Route.Stops);
        Assert.Equal(3, site.Route.Stops.Count);

        var anchors = new RouteAnchors();
        anchors.AddAddress(Oak, new TileCoord(10, 0));
        anchors.AddDistrict(2, new TileCoord(2, 0));
        anchors.AddConstruct(port, new TileCoord(6, 0));
        var graph = LineGraph();
        Assert.True(site.Route.TryRoundTrip(graph, site.ParkingZone, anchors, out var trip));
        Assert.Equal(reversed, trip.Stops);
        Assert.Equal(new[] { 0, 2, 1, 3, 0 }, trip.HopNodes);
        Assert.True(trip.LengthTiles > 0);
    }

    private static RoutingGraph LineGraph()
    {
        var nodes = new[]
        {
            new RouteNodeRecord(0, new TileCoord(6, 6)),
            new RouteNodeRecord(1, new TileCoord(2, 0)),
            new RouteNodeRecord(2, new TileCoord(6, 0)),
            new RouteNodeRecord(3, new TileCoord(10, 0))
        };
        var edges = new[]
        {
            new RouteEdgeRecord(0, 2, 6, 0),
            new RouteEdgeRecord(1, 2, 4, 0),
            new RouteEdgeRecord(2, 3, 4, 0)
        };
        return new RoutingGraph(nodes, edges);
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
            field ?? PlacementField.Flat(14, 12, TileCm),
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
