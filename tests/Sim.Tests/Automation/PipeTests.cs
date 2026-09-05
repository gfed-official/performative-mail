using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class PipeTests
{
    private static readonly TileCoord Inlet = new(2, 2);
    private static readonly TileCoord Pipe32 = new(3, 2);
    private static readonly TileCoord Pipe42 = new(4, 2);
    private static readonly TileCoord OutletA = new(5, 2);
    private static readonly TileCoord Pipe33 = new(3, 3);
    private static readonly TileCoord OutletB = new(3, 4);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId GlassId = new(5);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void RepoDefs_PipeFamily_MatchesChapterCostsAndBpPipes()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());
        var shop = Index(LoadShop());

        AssertPipeBuilding(buildings[PipeNetwork.PipeId], "recipe_pipe", 100, dragLine: true);
        AssertPipeBuilding(buildings[PipeNetwork.InletId], "recipe_pipe_inlet", 150, dragLine: false);
        AssertPipeBuilding(buildings[PipeNetwork.OutletId], "recipe_pipe_outlet", 120, dragLine: false);
        AssertPipeBuilding(buildings[PipeNetwork.JunctionId], "recipe_pipe_junction", 120, dragLine: false);
        AssertPipeBuilding(
            buildings[PipeNetwork.UndergroundId],
            "recipe_pipe_underground",
            200,
            dragLine: true,
            onStreet: true);

        var pipe = recipes["recipe_pipe"];
        Assert.Equal(PipeNetwork.PipeId, pipe.ProducesBuilding);
        Assert.Equal("bp_pipes", pipe.Blueprint);
        Assert.Equal("iron_ingot", pipe.Inputs[0].Item);
        Assert.Equal(1, pipe.Inputs[0].Count);
        Assert.Equal("glass", pipe.Inputs[1].Item);
        Assert.Equal(1, pipe.Inputs[1].Count);

        var inlet = recipes["recipe_pipe_inlet"];
        Assert.Equal(PipeNetwork.InletId, inlet.ProducesBuilding);
        Assert.Equal("bp_pipes", inlet.Blueprint);
        Assert.Equal("iron_ingot", inlet.Inputs[0].Item);
        Assert.Equal(3, inlet.Inputs[0].Count);
        Assert.Equal("glass", inlet.Inputs[1].Item);
        Assert.Equal(1, inlet.Inputs[1].Count);

        var outlet = recipes["recipe_pipe_outlet"];
        Assert.Equal(PipeNetwork.OutletId, outlet.ProducesBuilding);
        Assert.Equal("bp_pipes", outlet.Blueprint);
        Assert.Equal("iron_ingot", outlet.Inputs[0].Item);
        Assert.Equal(2, outlet.Inputs[0].Count);
        Assert.Single(outlet.Inputs);

        var junction = recipes["recipe_pipe_junction"];
        Assert.Equal(PipeNetwork.JunctionId, junction.ProducesBuilding);
        Assert.Equal("bp_pipes", junction.Blueprint);
        Assert.Equal("iron_ingot", junction.Inputs[0].Item);
        Assert.Equal(2, junction.Inputs[0].Count);
        Assert.Equal("glass", junction.Inputs[1].Item);
        Assert.Equal(1, junction.Inputs[1].Count);

        var underground = recipes["recipe_pipe_underground"];
        Assert.Equal(PipeNetwork.UndergroundId, underground.ProducesBuilding);
        Assert.Equal("bp_pipes", underground.Blueprint);
        Assert.Equal("iron_ingot", underground.Inputs[0].Item);
        Assert.Equal(2, underground.Inputs[0].Count);
        Assert.Equal("glass", underground.Inputs[1].Item);
        Assert.Equal(2, underground.Inputs[1].Count);

        Assert.True(shop.TryGetValue("bp_pipes", out var row));
        Assert.Equal(ShopKind.Blueprint, row.Kind);
        Assert.Equal(700, row.Price);
        Assert.Equal(3, row.FromShift);
        Assert.True(row.OncePerRun);
        Assert.Equal("bp_pipes", row.GrantBlueprint);
    }

    [Fact]
    public void Place_PipeInletOutlet_ConsumesRecipes()
    {
        var fx = Loaded(iron: 6, glass: 2);
        Assert.IsType<Placed>(fx.Registry.TryPlace(PipeNetwork.PipeId, new TileCoord(1, 1), Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(PipeNetwork.InletId, new TileCoord(2, 1), Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(PipeNetwork.OutletId, new TileCoord(3, 1), Facing.East, Owner));
        Assert.Equal(3, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, IronId));
        Assert.Equal(0, CountItem(fx, GlassId));

        var street = Loaded(iron: 1, glass: 1, field: PlacementField.Flat(8, 8, 200).WithStreet(new TileCoord(1, 1)));
        var rejected = Assert.IsType<PlaceRejected>(
            street.Registry.TryPlace(PipeNetwork.PipeId, new TileCoord(1, 1), Facing.East, Owner));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(1, CountItem(street, IronId));
        Assert.Equal(1, CountItem(street, GlassId));
    }

    [Fact]
    public void Place_JunctionAndUnderground_ConsumesRecipesAndSpansStreet()
    {
        var fx = Loaded(iron: 4, glass: 3);
        Assert.IsType<Placed>(fx.Registry.TryPlace(PipeNetwork.JunctionId, new TileCoord(1, 1), Facing.East, Owner));
        Assert.IsType<Placed>(
            fx.Registry.TryPlace(PipeNetwork.UndergroundId, new TileCoord(2, 1), Facing.East, Owner));
        Assert.Equal(2, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, IronId));
        Assert.Equal(0, CountItem(fx, GlassId));

        var streetTile = new TileCoord(1, 1);
        var street = Loaded(iron: 2, glass: 2, field: PlacementField.Flat(8, 8, 200).WithStreet(streetTile));
        Assert.IsType<Placed>(street.Registry.TryPlace(PipeNetwork.UndergroundId, streetTile, Facing.East, Owner));
        Assert.Equal(0, CountItem(street, IronId));
        Assert.Equal(0, CountItem(street, GlassId));
    }

    [Fact]
    public void Shop_BpPipes_OncePerRunFromRepo()
    {
        var defs = LoadShop();
        var wallet = new Wallet(new Cents(700));
        var shop = new ShopSession(defs, wallet, seed: 1);
        shop.RollOffers(3);

        var first = Assert.IsType<ShopBought>(shop.TryBuy("bp_pipes"));
        Assert.Equal("bp_pipes", first.Blueprint);
        Assert.Equal(new Cents(700), first.Paid);
        Assert.Contains("bp_pipes", shop.OwnedBlueprints);

        var second = Assert.IsType<ShopRejected>(shop.TryBuy("bp_pipes"));
        Assert.Equal(ShopReject.AlreadyBought, second.Reason);
        Assert.Single(shop.OwnedBlueprints);
    }

    [Fact]
    public void Compile_ForkGraph_HasInletAndTwoOutlets()
    {
        var pipes = CompileFork();
        Assert.Equal(Inlet, Assert.Single(pipes.Inlets));
        Assert.Equal(2, pipes.Outlets.Count);
        Assert.Contains(OutletA, pipes.Outlets);
        Assert.Contains(OutletB, pipes.Outlets);
        Assert.Empty(pipes.Capsules);
    }

    [Fact]
    public void Accept_MatchingStreet_ExitsChosenOutlet()
    {
        var pipes = RoutedFork();
        Assert.True(pipes.TryAccept(Inlet, 11, MailKinds.Letter, Address(street: 4)));

        pipes.StepTicks(35);
        Assert.Single(pipes.Capsules);
        Assert.Empty(pipes.Emitted(OutletA));
        Assert.Empty(pipes.Emitted(OutletB));

        pipes.StepTicks(1);
        Assert.Empty(pipes.Capsules);
        Assert.Equal(11, Assert.Single(pipes.Emitted(OutletA)).ItemId);
        Assert.Empty(pipes.Emitted(OutletB));
    }

    [Fact]
    public void Accept_Unmatched_ExitsDefaultOutlet()
    {
        var pipes = RoutedFork();
        Assert.True(pipes.TryAccept(Inlet, 12, MailKinds.Letter, Address(street: 9)));
        pipes.StepTicks(36);
        Assert.Empty(pipes.Capsules);
        Assert.Empty(pipes.Emitted(OutletA));
        Assert.Equal(12, Assert.Single(pipes.Emitted(OutletB)).ItemId);
    }

    [Fact]
    public void Accept_KindFilter_MapsToOutlet()
    {
        var pipes = CompileFork();
        Assert.True(pipes.SetFilter(Inlet, AddressFilter.ForKind(MailKinds.Postcard), OutletA));
        Assert.True(pipes.SetDefaultOutlet(Inlet, OutletB));

        Assert.True(pipes.TryAccept(Inlet, 21, MailKinds.Postcard, Address(street: 4)));
        pipes.StepTicks(36);
        Assert.Equal(21, Assert.Single(pipes.Emitted(OutletA)).ItemId);
        Assert.Empty(pipes.Emitted(OutletB));

        Assert.True(pipes.TryAccept(Inlet, 22, MailKinds.Letter, Address(street: 4)));
        pipes.StepTicks(36);
        Assert.Equal(22, Assert.Single(pipes.Emitted(OutletB)).ItemId);
        Assert.Single(pipes.Emitted(OutletA));
    }

    [Fact]
    public void Accept_CloserThanOneMetre_Rejected()
    {
        var pipes = RoutedFork();
        Assert.True(pipes.TryAccept(Inlet, 31, MailKinds.Letter, Address(street: 4)));
        Assert.False(pipes.TryAccept(Inlet, 32, MailKinds.Letter, Address(street: 4)));
        Assert.False(pipes.TryAccept(Inlet, 33, MailKinds.Letter, Address(street: 9)));

        while (Assert.Single(pipes.Capsules).MetresAlongPath < PipeNetwork.MinSpacingMetres)
            pipes.StepTicks(1);

        Assert.True(pipes.TryAccept(Inlet, 32, MailKinds.Letter, Address(street: 4)));
        Assert.Equal(2, pipes.Capsules.Count);
    }

    [Fact]
    public void Accept_UndergroundStreetSpan_ExitsOutlet()
    {
        var streetTile = new TileCoord(3, 2);
        var fx = Loaded(iron: 8, glass: 6, field: PlacementField.Flat(8, 8, 200).WithStreet(streetTile));
        var exit = Pipe42;
        Place(fx, PipeNetwork.InletId, Inlet);
        Place(fx, PipeNetwork.UndergroundId, streetTile);
        Place(fx, PipeNetwork.OutletId, exit);
        var pipes = new PipeNetwork();
        pipes.Compile(fx.Registry.All);
        Assert.True(pipes.SetDefaultOutlet(Inlet, exit));
        Assert.True(pipes.TryAccept(Inlet, 51, MailKinds.Letter, Address(street: 4)));
        pipes.StepTicks(23);
        Assert.Single(pipes.Capsules);
        pipes.StepTicks(1);
        Assert.Empty(pipes.Capsules);
        Assert.Equal(51, Assert.Single(pipes.Emitted(exit)).ItemId);
    }

    [Fact]
    public void Accept_VerticalOverBuilding_ExitsOutlet()
    {
        var fx = Loaded(iron: 5, glass: 2, logs: 3);
        var exit = Pipe42;
        Place(fx, PipeNetwork.InletId, Inlet);
        Assert.IsType<Placed>(fx.Registry.TryPlace("wall_wood", Pipe32, Facing.East, Owner));
        Place(fx, PipeNetwork.OutletId, exit);
        var pipes = new PipeNetwork();
        pipes.Compile(fx.Registry.All);
        Assert.True(pipes.SetDefaultOutlet(Inlet, exit));
        Assert.True(pipes.TryAccept(Inlet, 61, MailKinds.Letter, Address(street: 4)));
        pipes.StepTicks(24);
        Assert.Empty(pipes.Capsules);
        Assert.Equal(61, Assert.Single(pipes.Emitted(exit)).ItemId);
    }

    [Fact]
    public void Accept_ChestBetween_HasNoPath()
    {
        var exit = Pipe42;
        var fx = Loaded(iron: 5, glass: 2, logs: 4);
        Place(fx, PipeNetwork.InletId, Inlet);
        Assert.IsType<Placed>(fx.Registry.TryPlace("chest", Pipe32, Facing.East, Owner));
        Place(fx, PipeNetwork.OutletId, exit);
        var pipes = new PipeNetwork();
        pipes.Compile(fx.Registry.All);
        Assert.True(pipes.SetDefaultOutlet(Inlet, exit));
        Assert.False(pipes.TryAccept(Inlet, 62, MailKinds.Letter, Address(street: 4)));
    }

    [Fact]
    public void Accept_BlockedOutlet_RoutesAroundJunction()
    {
        var pipes = RoutedJunction();
        Assert.True(pipes.SetOutletBlocked(OutletA, true));
        Assert.True(pipes.TryAccept(Inlet, 71, MailKinds.Letter, Address(street: 4)));
        pipes.StepTicks(36);
        Assert.Empty(pipes.Capsules);
        Assert.Empty(pipes.Emitted(OutletA));
        Assert.Equal(71, Assert.Single(pipes.Emitted(OutletB)).ItemId);
    }

    [Fact]
    public void Step_BlockedOutlet_DoesNotDropCapsule()
    {
        var pipes = RoutedJunction();
        Assert.True(pipes.TryAccept(Inlet, 81, MailKinds.Letter, Address(street: 4)));
        Assert.True(pipes.SetOutletBlocked(OutletA, true));
        Assert.True(pipes.SetOutletBlocked(OutletB, true));
        pipes.StepTicks(36);
        Assert.Equal(81, Assert.Single(pipes.Capsules).ItemId);
        Assert.Empty(pipes.Emitted(OutletA));
        Assert.Empty(pipes.Emitted(OutletB));
    }

    [Fact]
    public void Route_FirstMatchingFilterWins()
    {
        var pipes = CompileFork();
        var both = AddressFilter.ForDistrict(1);
        Assert.True(pipes.SetFilter(Inlet, both, OutletA));
        Assert.True(pipes.SetFilter(Inlet, both, OutletB));
        Assert.True(pipes.SetDefaultOutlet(Inlet, OutletB));
        var item = new BeltItem(41, 0f, MailKinds.Letter, Address(street: 4));
        Assert.Equal(OutletA, pipes.Route(Inlet, item));
    }

    private static void AssertPipeBuilding(
        BuildingDef building,
        string recipe,
        int hp,
        bool dragLine,
        bool onStreet = false)
    {
        Assert.Equal(recipe, building.Recipe);
        Assert.Equal(hp, building.Hp);
        Assert.Equal(1, building.Footprint.W);
        Assert.Equal(1, building.Footprint.H);
        Assert.Equal(4, building.Rotations);
        Assert.Equal(onStreet, building.OnStreet);
        Assert.Equal(WaterPlacement.None, building.OnWater);
        Assert.Equal(15, building.MaxSlopeDeg);
        Assert.Equal(dragLine, building.DragLine);
        Assert.Equal(BuildingBehaviour.Pipe, building.Behaviour);
    }

    private static PipeNetwork RoutedFork()
    {
        var pipes = CompileFork();
        Assert.True(pipes.SetFilter(Inlet, AddressFilter.ForStreet(4), OutletA));
        Assert.True(pipes.SetDefaultOutlet(Inlet, OutletB));
        return pipes;
    }

    private static PipeNetwork CompileFork()
    {
        var fx = Loaded(iron: 12, glass: 6, field: PlacementField.Flat(8, 8, 200));
        Place(fx, PipeNetwork.InletId, Inlet);
        Place(fx, PipeNetwork.PipeId, Pipe32);
        Place(fx, PipeNetwork.PipeId, Pipe42);
        Place(fx, PipeNetwork.OutletId, OutletA);
        Place(fx, PipeNetwork.PipeId, Pipe33);
        Place(fx, PipeNetwork.OutletId, OutletB);
        var pipes = new PipeNetwork();
        pipes.Compile(fx.Registry.All);
        return pipes;
    }

    private static PipeNetwork RoutedJunction()
    {
        var fx = Loaded(iron: 13, glass: 6, field: PlacementField.Flat(8, 8, 200));
        Place(fx, PipeNetwork.InletId, Inlet);
        Place(fx, PipeNetwork.JunctionId, Pipe32);
        Place(fx, PipeNetwork.PipeId, Pipe42);
        Place(fx, PipeNetwork.OutletId, OutletA);
        Place(fx, PipeNetwork.PipeId, Pipe33);
        Place(fx, PipeNetwork.OutletId, OutletB);
        var pipes = new PipeNetwork();
        pipes.Compile(fx.Registry.All);
        Assert.True(pipes.SetFilter(Inlet, AddressFilter.ForStreet(4), OutletA));
        Assert.True(pipes.SetDefaultOutlet(Inlet, OutletB));
        return pipes;
    }

    private static void Place(Fixture fx, string id, TileCoord tile)
        => Assert.IsType<Placed>(fx.Registry.TryPlace(id, tile, Facing.East, Owner));

    private static AddressId Address(byte street, byte district = 1, byte number = 13, byte unit = 0)
        => new(district, street, number, unit);

    private static Fixture Loaded(int iron = 0, int glass = 0, int logs = 0, PlacementField? field = null)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        if (glass > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(GlassId, glass))));
        if (logs > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, logs))));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(8, 8, 200),
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
        ["log"] = LogId,
        ["iron_ingot"] = IronId,
        ["glass"] = GlassId
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
            return new Footprint(1, 1);
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return 20;
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) throw new ArgumentException("Unknown stack key.", nameof(key));
            return WeightClass.Light;
        }

        public StackCategory CategoryOf(StackKey key) => StackCategory.Material;
    }
}
