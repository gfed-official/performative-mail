using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class InserterTests
{
    private static readonly TileCoord ChestTile = new(1, 1);
    private static readonly TileCoord InserterTile = new(2, 1);
    private static readonly TileCoord BeltTile = new(3, 1);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly AddressId Oak = new(1, 4, 13, 0);

    [Fact]
    public void RepoDefs_InserterMk1_MatchesChapterCosts()
    {
        var buildings = Index(LoadBuildings());
        var recipes = Index(LoadRecipes());

        Assert.True(buildings.TryGetValue(Inserter.BuildingId, out var inserter));
        Assert.True(recipes.TryGetValue(inserter.Recipe, out var recipe));
        Assert.Equal("recipe_inserter", inserter.Recipe);
        Assert.Equal(Inserter.BuildingId, recipe.ProducesBuilding);
        Assert.Equal(120, inserter.Hp);
        Assert.Equal(1, inserter.Footprint.W);
        Assert.Equal(1, inserter.Footprint.H);
        Assert.Equal(4, inserter.Rotations);
        Assert.False(inserter.OnStreet);
        Assert.Equal(WaterPlacement.None, inserter.OnWater);
        Assert.Equal(15, inserter.MaxSlopeDeg);
        Assert.False(inserter.DragLine);
        Assert.Equal(BuildingBehaviour.Inserter, inserter.Behaviour);
        Assert.Null(inserter.Container);
        Assert.Null(recipe.Blueprint);
        Assert.Equal("iron_ingot", recipe.Inputs[0].Item);
        Assert.Equal(2, recipe.Inputs[0].Count);
        Assert.Equal("plank", recipe.Inputs[1].Item);
        Assert.Equal(1, recipe.Inputs[1].Count);
        Assert.Equal(24, Inserter.TransferPeriodTicks);
        Assert.Equal(TickClock.TickHz * 4 / 5, Inserter.TransferPeriodTicks);
    }

    [Fact]
    public void Place_ConsumesRecipe_AndOccupiesOneTile()
    {
        var fx = Loaded(planks: 1, iron: 2);
        var placed = Assert.IsType<Placed>(
            fx.Registry.TryPlace(Inserter.BuildingId, InserterTile, Facing.East, Owner));

        Assert.Equal(Inserter.BuildingId, placed.Construct.DefId);
        Assert.Equal(1, fx.Registry.Count);
        Assert.Equal(0, CountItem(fx, IronId));
        Assert.Equal(0, CountItem(fx, PlankId));
        Assert.IsType<PlaceRejected>(fx.Registry.TryPlace(Inserter.BuildingId, InserterTile, Facing.East, Owner));
    }

    [Fact]
    public void Place_StreetTile_Rejects()
    {
        var fx = Loaded(planks: 1, iron: 2, field: PlacementField.Flat(8, 6, 200).WithStreet(InserterTile));
        var rejected = Assert.IsType<PlaceRejected>(
            fx.Registry.TryPlace(Inserter.BuildingId, InserterTile, Facing.East, Owner));
        Assert.Equal(PlaceReject.Street, rejected.Reason);
        Assert.Equal(2, CountItem(fx, IronId));
        Assert.Equal(1, CountItem(fx, PlankId));
    }

    [Fact]
    public void Pull_ChestOntoBelt_TransfersAfterPeriod()
    {
        var fx = Layout();
        var mailId = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Chest, MailStack.Single(MailKinds.Letter, Oak, mailId))));

        fx.Inserters.StepTicks(fx.Belts, Inserter.TransferPeriodTicks);

        var item = Assert.Single(Sink(fx).Lane(0));
        Assert.Equal((int)mailId.Value, item.ItemId);
        Assert.InRange(item.MetresFromStart, 0f, 0.01f);
        Assert.False(ContainsMail(fx.Inv, fx.Chest, mailId));
    }

    [Fact]
    public void KindFilter_SkipsNonMatchingHead()
    {
        var fx = Layout();
        var postcard = fx.RegisterPostcard(Oak);
        var letter = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Chest, MailStack.Single(MailKinds.Postcard, Oak, postcard))));
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Chest, MailStack.Single(MailKinds.Letter, Oak, letter))));
        Assert.True(HeadKind(fx.Inv, fx.Chest).Equals(MailKinds.Postcard));
        Assert.True(fx.Inserters.SetFilter(InserterTile, MailKinds.Letter));

        fx.Inserters.StepTicks(fx.Belts, Inserter.TransferPeriodTicks);
        Assert.True(ContainsMail(fx.Inv, fx.Chest, postcard));
        Assert.True(ContainsMail(fx.Inv, fx.Chest, letter));
        Assert.Equal(0, CountBeltItems(fx.Belts));

        WithdrawMail(fx.Inv, fx.Chest, postcard);
        Assert.True(HeadKind(fx.Inv, fx.Chest).Equals(MailKinds.Letter));

        fx.Inserters.StepTicks(fx.Belts, Inserter.TransferPeriodTicks);
        Assert.False(ContainsMail(fx.Inv, fx.Chest, letter));
        Assert.Equal((int)letter.Value, Assert.Single(Sink(fx).Lane(0)).ItemId);
    }

    [Fact]
    public void Rate_OneItemPerPointEightSeconds()
    {
        var fx = Layout();
        var first = fx.RegisterLetter(Oak);
        var second = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Chest, MailStack.Single(MailKinds.Letter, Oak, first))));
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Chest, MailStack.Single(MailKinds.Letter, Oak, second))));

        fx.Inserters.StepTicks(fx.Belts, Inserter.TransferPeriodTicks - 1);
        Assert.Equal(0, CountBeltItems(fx.Belts));
        Assert.Equal(2, CountMail(fx.Inv, fx.Chest));

        fx.Inserters.StepTicks(fx.Belts, 1);
        Assert.Equal(1, CountBeltItems(fx.Belts));
        Assert.Equal(1, CountMail(fx.Inv, fx.Chest));

        fx.Inserters.StepTicks(fx.Belts, Inserter.TransferPeriodTicks - 1);
        Assert.Equal(1, CountBeltItems(fx.Belts));
        Assert.Equal(1, CountMail(fx.Inv, fx.Chest));

        fx.Inserters.StepTicks(fx.Belts, 1);
        Assert.Equal(2, CountBeltItems(fx.Belts));
        Assert.Equal(0, CountMail(fx.Inv, fx.Chest));
    }

    [Fact]
    public void Transfer_WaitsFullPeriod_ThenCompletes()
    {
        var inserter = new Inserter(InserterTile, Facing.East);
        var item = new BeltItem(1, 0f, MailKinds.Letter, Oak);

        inserter.StepTicks(Inserter.TransferPeriodTicks - 1, item);
        Assert.Empty(inserter.Emitted);

        inserter.StepTicks(1, item);
        Assert.Equal(item.ItemId, Assert.Single(inserter.Emitted).ItemId);
    }

    private static Fixture Layout()
    {
        var fx = Loaded(planks: 2, iron: 3, logs: 4);
        Assert.IsType<Placed>(fx.Registry.TryPlace("chest", ChestTile, Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(Inserter.BuildingId, InserterTile, Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, BeltTile, Facing.East, Owner));

        var chest = fx.Inv.CreateContainer(ContainerSpec.Chest);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var inserters = new InserterNetwork();
        inserters.BindInventory(fx.Inv, fx.Mail);
        inserters.BindChest(ChestTile, chest);
        inserters.Compile(fx.Registry.All, belts);
        Assert.Single(inserters.Inserters);
        return fx.With(chest, belts, inserters);
    }

    private static BeltSegment Sink(Fixture fx) => SegmentStartingAt(fx.Belts, BeltTile, Facing.East);

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

    private static MailKindId HeadKind(InventorySystem inv, ContainerId chest)
    {
        Assert.True(inv.TryGetContainer(chest, out var grid));
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is MailStack mail)
                return mail.Kind;
        }

        throw new InvalidOperationException("chest has no mail head");
    }

    private static void WithdrawMail(InventorySystem inv, ContainerId chest, MailId id)
    {
        Assert.True(inv.TryGetContainer(chest, out var grid));
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is not MailStack mail) continue;
            for (int i = 0; i < mail.Ids.Count; i++)
            {
                if (!mail.Ids[i].Equals(id)) continue;
                Assert.IsType<Accepted>(inv.Apply(Actor.System, new Withdraw(chest, entry.Id)));
                return;
            }
        }

        throw new InvalidOperationException("mail not in chest");
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

    private static int CountMail(InventorySystem inv, ContainerId box)
    {
        if (!inv.TryGetContainer(box, out var grid)) return 0;
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is MailStack mail)
                n += mail.Count;
        }

        return n;
    }

    private static int CountBeltItems(BeltNetwork belts)
    {
        int n = 0;
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            n += segment.Lane(0).Count;
            n += segment.Lane(1).Count;
        }

        return n;
    }

    private static Fixture Loaded(int planks = 0, int iron = 0, int logs = 0, PlacementField? field = null)
    {
        var catalog = new InserterCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (logs > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, logs))));
        if (planks > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, planks))));
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        var mail = new MailRegistry();
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field ?? PlacementField.Flat(8, 6, 200),
            inv,
            bag,
            Ids());
        return new Fixture(registry, inv, bag, mail, default, null!, null!);
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

    private readonly record struct Fixture(
        ConstructRegistry Registry,
        InventorySystem Inv,
        ContainerId Bag,
        MailRegistry Mail,
        ContainerId Chest,
        BeltNetwork Belts,
        InserterNetwork Inserters)
    {
        public Fixture With(ContainerId chest, BeltNetwork belts, InserterNetwork inserters)
            => this with { Chest = chest, Belts = belts, Inserters = inserters };

        public MailId RegisterLetter(AddressId address)
        {
            var id = Mail.Allocate();
            Assert.True(Mail.Register(new MailItem(id, MailKinds.Letter, address, MailKinds.LetterBaseValue, 1, 1)));
            return id;
        }

        public MailId RegisterPostcard(AddressId address)
        {
            var id = Mail.Allocate();
            Assert.True(Mail.Register(new MailItem(id, MailKinds.Postcard, address, MailKinds.PostcardBaseValue, 1, 1)));
            return id;
        }
    }

    private sealed class InserterCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.FootprintOf(key);
            if (key.Def == LogId.Value) return new Footprint(1, 2);
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return new Footprint(1, 1);
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public int MaxStackOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.MaxStackOf(key);
            if (key.Def == LogId.Value) return 10;
            if (key.Def == PlankId.Value || key.Def == IronId.Value) return 20;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (key.IsMail) return MailStackCatalog.Default.WeightOf(key);
            if (key.Def == LogId.Value || key.Def == PlankId.Value || key.Def == IronId.Value)
                return WeightClass.Light;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public StackCategory CategoryOf(StackKey key)
            => key.IsMail ? StackCategory.Mail : StackCategory.Material;
    }
}
