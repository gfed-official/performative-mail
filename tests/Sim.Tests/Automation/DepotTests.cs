using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class DepotTests
{
    private static readonly TileCoord Origin = new(5, 5);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId StoneId = new(4);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly AddressId Oak = new(1, 4, 13, 0);

    [Fact]
    public void RepoDefs_Depot_MatchesChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());
        var containers = Index(LoadContainers());

        Assert.True(buildings.TryGetValue(Depot.BuildingId, out var depot));
        Assert.True(recipes.TryGetValue(depot.Recipe, out var recipe));
        Assert.True(containers.TryGetValue(Depot.BuildingId, out var grid));
        Assert.Equal("recipe_depot", depot.Recipe);
        Assert.Equal(Depot.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(Depot.BuildingId, depot.Container);
        Assert.Equal(800, depot.Hp);
        Assert.Equal(2, depot.Footprint.W);
        Assert.Equal(2, depot.Footprint.H);
        Assert.Equal(4, depot.Rotations);
        Assert.False(depot.OnStreet);
        Assert.Equal(WaterPlacement.None, depot.OnWater);
        Assert.Equal(15, depot.MaxSlopeDeg);
        Assert.False(depot.DragLine);
        Assert.Equal(BuildingBehaviour.Container, depot.Behaviour);
        Assert.Null(recipe.Blueprint);
        Assert.Equal("stone", recipe.Inputs[0].Item);
        Assert.Equal(8, recipe.Inputs[0].Count);
        Assert.Equal("iron_ingot", recipe.Inputs[1].Item);
        Assert.Equal(4, recipe.Inputs[1].Count);
        Assert.Equal("plank", recipe.Inputs[2].Item);
        Assert.Equal(4, recipe.Inputs[2].Count);
        Assert.Equal(16, grid.Grid.W);
        Assert.Equal(10, grid.Grid.H);
        Assert.Equal(ContainerView.Grid, grid.View);
        Assert.Equal(BeltAccess.AnySide, grid.BeltAccess);
        Assert.Equal(16, Depot.Spec.Shape.Cols);
        Assert.Equal(10, Depot.Spec.Shape.Rows);
        Assert.Equal(160, Depot.Spec.Shape.CellCount);
    }

    [Fact]
    public void RepoDefs_PoDepot_IsPrebuiltLargerFace()
    {
        var buildings = Index(LoadBuildings());
        var containers = Index(LoadContainers());

        Assert.False(buildings.ContainsKey("po_depot"));
        Assert.True(containers.TryGetValue("po_depot", out var po));
        Assert.Equal(20, po.Grid.W);
        Assert.Equal(16, po.Grid.H);
        Assert.Equal(ContainerView.Manifest, po.View);
        Assert.Equal(BeltAccess.AnySide, po.BeltAccess);
        Assert.Equal(20, ContainerSpec.Depot.Shape.Cols);
        Assert.Equal(16, ContainerSpec.Depot.Shape.Rows);
        Assert.Equal(320, ContainerSpec.Depot.Shape.CellCount);
    }

    [Fact]
    public void Place_ConsumesRecipe_AndOccupiesFourTiles()
    {
        var fx = Loaded(stone: 8, iron: 4, planks: 4);
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(Depot.BuildingId, Origin, Facing.East, Owner));

        Assert.Equal(Depot.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, StoneId));
        Assert.Equal(0, CountItem(fx, IronId));
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(Depot.BuildingId, Origin, Facing.East, Owner));
        Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(Depot.BuildingId, new TileCoord(6, 6), Facing.North, Owner));
    }

    [Fact]
    public void Place_StreetTile_Rejects()
    {
        var fx = Loaded(
            stone: 8,
            iron: 4,
            planks: 4,
            field: PlacementField.Flat(12, 10, 200).WithStreet(Origin));
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(Depot.BuildingId, Origin, Facing.East, Owner));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(8, CountItem(fx, StoneId));
    }

    [Theory]
    [InlineData(4, 5, Facing.East)]
    [InlineData(7, 5, Facing.West)]
    [InlineData(5, 4, Facing.North)]
    [InlineData(5, 7, Facing.South)]
    public void Drain_BeltOnAnySide_Inserts(int beltX, int beltY, Facing facing)
    {
        var fx = PlaceDepot(stone: 8, iron: 5, planks: 5);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(beltX, beltY), facing);
        var box = BindDepot(fx, Depot.Spec);
        var belts = Compile(fx);
        var segment = SegmentEndingAt(belts, BeltNetwork.Next(new TileCoord(beltX, beltY), facing));
        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);

        Assert.Empty(segment.Lane(0));
        Assert.True(ContainsMail(fx.Inv, box, mailId));
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Theory]
    [InlineData(4, 5, Facing.East)]
    [InlineData(7, 5, Facing.West)]
    [InlineData(5, 4, Facing.North)]
    [InlineData(5, 7, Facing.South)]
    public void Drain_PoDepot_BeltOnAnySide_Inserts(int beltX, int beltY, Facing facing)
    {
        var fx = Loaded(iron: 1, planks: 1);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(beltX, beltY), facing);
        var box = BindDepot(fx, ContainerSpec.Depot);
        var belts = Compile(fx);
        var segment = SegmentEndingAt(belts, BeltNetwork.Next(new TileCoord(beltX, beltY), facing));
        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);

        Assert.Empty(segment.Lane(0));
        Assert.True(ContainsMail(fx.Inv, box, mailId));
    }

    [Theory]
    [InlineData(4, 5, Facing.West)]
    [InlineData(7, 5, Facing.East)]
    [InlineData(5, 4, Facing.South)]
    [InlineData(5, 7, Facing.North)]
    public void Pull_InserterOnAnySide_Withdraws(int inserterX, int inserterY, Facing facing)
    {
        var fx = PlaceDepot(stone: 8, iron: 7, planks: 6);
        var inserterTile = new TileCoord(inserterX, inserterY);
        var output = BeltNetwork.Next(inserterTile, facing);
        PlaceId(fx, Inserter.BuildingId, inserterTile, facing);
        PlaceId(fx, BeltNetwork.BuildingId, output, facing);
        var box = BindDepot(fx, Depot.Spec);
        var mailId = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(box, MailStack.Single(MailKinds.Letter, Oak, mailId))));

        var belts = Compile(fx);
        fx.Inserters.BindInventory(fx.Inv, fx.Mail);
        fx.Inserters.BindTiles(box, Depot.Occupied(Origin));
        fx.Inserters.Compile(fx.Registry.All, belts);
        fx.Inserters.StepTicks(belts, Inserter.TransferPeriodTicks);

        Assert.False(ContainsMail(fx.Inv, box, mailId));
        var segment = SegmentStartingAt(belts, output, facing);
        Assert.Equal((int)mailId.Value, Assert.Single(segment.Lane(0)).ItemId);
    }

    [Theory]
    [InlineData(4, 5, Facing.West)]
    [InlineData(7, 5, Facing.East)]
    [InlineData(5, 4, Facing.South)]
    [InlineData(5, 7, Facing.North)]
    public void Pull_PoDepot_InserterOnAnySide_Withdraws(int inserterX, int inserterY, Facing facing)
    {
        var fx = Loaded(iron: 3, planks: 2);
        var inserterTile = new TileCoord(inserterX, inserterY);
        var output = BeltNetwork.Next(inserterTile, facing);
        PlaceId(fx, Inserter.BuildingId, inserterTile, facing);
        PlaceId(fx, BeltNetwork.BuildingId, output, facing);
        var box = BindDepot(fx, ContainerSpec.Depot);
        var mailId = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(box, MailStack.Single(MailKinds.Letter, Oak, mailId))));

        var belts = Compile(fx);
        fx.Inserters.BindInventory(fx.Inv, fx.Mail);
        fx.Inserters.BindTiles(box, Depot.Occupied(Origin));
        fx.Inserters.Compile(fx.Registry.All, belts);
        fx.Inserters.StepTicks(belts, Inserter.TransferPeriodTicks);

        Assert.False(ContainsMail(fx.Inv, box, mailId));
        var segment = SegmentStartingAt(belts, output, facing);
        Assert.Equal((int)mailId.Value, Assert.Single(segment.Lane(0)).ItemId);
    }

    private static Fixture PlaceDepot(int stone, int iron, int planks)
    {
        var fx = Loaded(stone: stone, iron: iron, planks: planks);
        PlaceId(fx, Depot.BuildingId, Origin, Facing.East);
        return fx;
    }

    private static ContainerId BindDepot(Fixture fx, ContainerSpec spec)
    {
        var box = fx.Inv.CreateContainer(spec);
        fx.Endpoints.BindInventory(fx.Inv, fx.Mail);
        fx.Endpoints.BindTiles(box, Depot.Occupied(Origin));
        return box;
    }

    private static void PlaceId(Fixture fx, string id, TileCoord tile, Facing facing)
    {
        Assert.IsType<Placed>(fx.Registry.TryPlace(id, tile, facing, Owner));
    }

    private static BeltNetwork Compile(Fixture fx)
    {
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        return belts;
    }

    private static BeltSegment SegmentEndingAt(BeltNetwork belts, TileCoord ahead)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            if (belts.Segments[i].AheadTile.Equals(ahead))
                return belts.Segments[i];
        }

        throw new InvalidOperationException("missing input segment");
    }

    private static BeltSegment SegmentStartingAt(BeltNetwork belts, TileCoord tile, Facing facing)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            if (segment.Tiles.Count > 0 && segment.Tiles[0].Equals(tile) && segment.Facing == facing)
                return segment;
        }

        throw new InvalidOperationException("missing output segment");
    }

    private static bool ContainsMail(InventorySystem inv, ContainerId box, MailId id)
    {
        if (!inv.TryGetContainer(box, out var grid)) return false;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is not MailStack mail) continue;
            for (int i = 0; i < mail.Ids.Count; i++)
            {
                if (mail.Ids[i].Equals(id)) return true;
            }
        }

        return false;
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
        var mail = new MailRegistry();
        var destinations = new Destinations(mail);
        var house = new DestinationId(1);
        Assert.True(destinations.Register(new Destination(house, DestinationType.HouseMailbox, Oak)));
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(12, 10, 200),
            inv,
            bag,
            Ids());
        return new Fixture(
            registry,
            inv,
            bag,
            mail,
            destinations,
            house,
            new Wallet(),
            new BeltEndpoints(),
            new InserterNetwork());
    }

    private static BuildingDef[] LoadBuildings() =>
        BuildingCatalog.LoadDir(Path.Combine(FindContentRoot(), BuildingCatalog.RelativeDir));

    private static RecipeDef[] LoadRecipes() =>
        RecipeCatalog.LoadDir(Path.Combine(FindContentRoot(), RecipeCatalog.RelativeDir));

    private static ContainerDef[] LoadContainers() =>
        ContainerCatalog.LoadFile(Path.Combine(FindContentRoot(), ContainerCatalog.RelativePath));

    private static Dictionary<string, T> Index<T>(T[] defs) where T : class
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            string id = def switch
            {
                BuildingDef building => building.Id,
                RecipeDef recipe => recipe.Id,
                ContainerDef container => container.Id,
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

    private readonly record struct Fixture(
        ConstructRegistry Registry,
        InventorySystem Inv,
        ContainerId Bag,
        MailRegistry Mail,
        Destinations Destinations,
        DestinationId House,
        Wallet Wallet,
        BeltEndpoints Endpoints,
        InserterNetwork Inserters)
    {
        public MailId RegisterLetter(AddressId address)
        {
            var id = Mail.Allocate();
            Assert.True(Mail.Register(new MailItem(id, MailKinds.Letter, address, MailKinds.LetterBaseValue, 1, 1)));
            return id;
        }
    }

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
