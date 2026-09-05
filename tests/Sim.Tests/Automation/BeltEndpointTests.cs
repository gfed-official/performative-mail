using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Automation;

public sealed class BeltEndpointTests
{
    private static readonly TileCoord Origin = new(1, 1);
    private static readonly TileCoord JunctionOrigin = new(3, 2);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly AddressId Elm = new(1, 5, 2, 0);

    [Fact]
    public void Drain_MailboxMatch_TakesLetterAndCreditsWallet()
    {
        var fx = PlaceEast(1);
        fx.Endpoints.BindMailbox(new TileCoord(2, 1), fx.House);
        var belts = Compile(fx);
        var segment = Assert.Single(belts.Segments);
        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);

        Assert.Empty(segment.Lane(0));
        Assert.False(fx.Mail.Contains(mailId));
        Assert.Equal(new Cents(MailKinds.LetterBaseValue), fx.Wallet.Balance);
    }

    [Fact]
    public void Drain_HouseMailbox_RefusesCargo()
    {
        var fx = PlaceEast(1);
        fx.Endpoints.BindMailbox(new TileCoord(2, 1), fx.House);
        var belts = Compile(fx);
        var segment = Assert.Single(belts.Segments);
        var mailId = fx.RegisterCargo(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f, MailKinds.Cargo));

        belts.StepTicks(1);
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);

        Assert.Equal(mailId.Value, (uint)Assert.Single(segment.Lane(0)).ItemId);
        Assert.True(fx.Mail.Contains(mailId));
        Assert.Equal(new Cents(0), fx.Wallet.Balance);
    }

    [Fact]
    public void Drain_Chest_AcceptsLetter()
    {
        var fx = Loaded(planks: 1, iron: 1, logs: 4);
        PlaceId(fx, BeltNetwork.BuildingId, Origin, Facing.East);
        PlaceId(fx, "chest", new TileCoord(2, 1), Facing.East);
        var chest = fx.Inv.CreateContainer(ContainerSpec.Chest);
        fx.Endpoints.BindChest(new TileCoord(2, 1), chest);
        fx.Endpoints.BindIntake(fx.Intake, new TileCoord(0, 1), Facing.East, fx.Inv, fx.Mail);
        var belts = Compile(fx);
        var segment = Assert.Single(belts.Segments);
        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);

        Assert.Empty(segment.Lane(0));
        Assert.True(ContainsMail(fx.Inv, chest, mailId));
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void Drain_ChestFull_HeadStays()
    {
        var fx = Loaded(planks: 1, iron: 1, logs: 4);
        PlaceId(fx, BeltNetwork.BuildingId, Origin, Facing.East);
        PlaceId(fx, "chest", new TileCoord(2, 1), Facing.East);
        var chest = fx.Inv.CreateContainer(ContainerSpec.Chest);
        fx.Endpoints.BindChest(new TileCoord(2, 1), chest);
        fx.Endpoints.BindIntake(fx.Intake, new TileCoord(0, 1), Facing.East, fx.Inv, fx.Mail);
        FillChest(fx.Inv, chest);
        var belts = Compile(fx);
        var segment = Assert.Single(belts.Segments);
        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);

        Assert.Equal(mailId.Value, (uint)Assert.Single(segment.Lane(0)).ItemId);
        Assert.False(ContainsMail(fx.Inv, chest, mailId));
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void Drain_AirDrop_DespawnsToIntakeAfter300s()
    {
        var fx = PlaceEast(1);
        fx.Endpoints.BindIntake(fx.Intake, new TileCoord(0, 1), Facing.East, fx.Inv, fx.Mail);
        var belts = Compile(fx);
        var segment = Assert.Single(belts.Segments);
        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        uint tick = 0;
        fx.Endpoints.Drain(belts, tick, fx.Destinations, fx.Wallet);

        Assert.Empty(segment.Lane(0));
        var dropped = Assert.Single(fx.Endpoints.WorldItems);
        Assert.Equal((int)mailId.Value, dropped.ItemId);
        Assert.Equal(MailKinds.Letter, dropped.Kind);
        Assert.Equal(segment.AheadTile, dropped.Tile);
        Assert.Equal(tick + (uint)TickClock.TicksFromSeconds(BeltEndpoints.WorldItemDespawnSeconds), dropped.DespawnTick);

        fx.Endpoints.StepDespawn(tick + (uint)TickClock.TicksFromSeconds(299));
        Assert.Single(fx.Endpoints.WorldItems);
        Assert.False(ContainsMail(fx.Inv, fx.Intake, mailId));

        fx.Endpoints.StepDespawn(tick + (uint)TickClock.TicksFromSeconds(300));
        Assert.Empty(fx.Endpoints.WorldItems);
        Assert.True(ContainsMail(fx.Inv, fx.Intake, mailId));
        Assert.True(fx.Mail.Contains(mailId));
    }

    [Fact]
    public void Feed_Intake_PopsMinMailId()
    {
        var fx = Loaded(planks: 1, iron: 1);
        var beltTile = BeltNetwork.Next(Origin, Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, beltTile, Facing.East);
        fx.Endpoints.BindIntake(fx.Intake, Origin, Facing.East, fx.Inv, fx.Mail);
        var first = fx.RegisterLetter(Oak);
        var second = fx.RegisterLetter(Elm);
        Assert.True(first.Value < second.Value);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Intake, MailStack.Single(MailKinds.Letter, Oak, first))));
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(fx.Intake, MailStack.Single(MailKinds.Letter, Elm, second))));
        var belts = Compile(fx);
        var segment = Assert.Single(belts.Segments);

        fx.Endpoints.Feed(belts);

        Assert.Equal((int)first.Value, Assert.Single(segment.Lane(0)).ItemId);
        Assert.False(ContainsMail(fx.Inv, fx.Intake, first));
        Assert.True(ContainsMail(fx.Inv, fx.Intake, second));

        fx.Endpoints.Feed(belts);
        Assert.Equal((int)second.Value, Assert.Single(segment.Lane(1)).ItemId);
        Assert.False(ContainsMail(fx.Inv, fx.Intake, second));
    }

    [Fact]
    public void Occupancy_CargoOccupiesBothLanesAndTwoMetres()
    {
        var cargo = CompileEast(4);
        Assert.True(cargo.TryInsert(0, 1, 2.0f, MailKinds.Cargo));
        Assert.False(cargo.TryInsert(1, 2, 1.0f));
        Assert.False(cargo.TryInsert(0, 3, 1.5f));
        Assert.True(cargo.TryInsert(0, 4, 0.0f));
        Assert.False(cargo.TryInsert(0, 5, 2.0f, MailKinds.Cargo));

        var letters = CompileEast(4);
        Assert.True(letters.TryInsert(0, 6, 0.0f));
        Assert.True(letters.TryInsert(0, 7, 0.5f));
        Assert.Equal(2, letters.Lane(0).Count);
    }

    [Fact]
    public void Drain_JunctionInput_DoesNotAirDrop()
    {
        var fx = Loaded(planks: 5, iron: 6);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(2, 2), Facing.East);
        PlaceId(fx, BeltNetwork.SplitterId, JunctionOrigin, Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(4, 2), Facing.East);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(3, 3), Facing.North);
        PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(3, 1), Facing.South);
        var belts = Compile(fx);
        var input = SegmentOn(belts, new TileCoord(2, 2));
        Assert.True(input.FeedsJunction);
        Assert.True(input.TryInsert(0, 1, 2.0f));

        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);
        Assert.Empty(fx.Endpoints.WorldItems);
        Assert.Equal(1, Assert.Single(input.Lane(0)).ItemId);

        belts.StepTicks(1);
        Assert.Empty(input.Lane(0));
        fx.Endpoints.Drain(belts, 0, fx.Destinations, fx.Wallet);
        Assert.Empty(fx.Endpoints.WorldItems);
    }

    private static BeltNetwork Compile(Fixture fx)
    {
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        return belts;
    }

    private static BeltSegment CompileEast(int tiles)
    {
        var fx = PlaceEast(tiles);
        return Assert.Single(Compile(fx).Segments);
    }

    private static Fixture PlaceEast(int tiles)
    {
        var fx = Loaded(planks: tiles, iron: tiles);
        for (int i = 0; i < tiles; i++)
            PlaceId(fx, BeltNetwork.BuildingId, new TileCoord(1 + i, 1), Facing.East);
        return fx;
    }

    private static void PlaceId(Fixture fx, string id, TileCoord tile, Facing facing)
    {
        Assert.IsType<Placed>(fx.Registry.TryPlace(id, tile, facing, Owner));
    }

    private static BeltSegment SegmentOn(BeltNetwork belts, TileCoord tile)
    {
        foreach (var segment in belts.Segments)
        {
            for (int i = 0; i < segment.Tiles.Count; i++)
            {
                if (segment.Tiles[i].Equals(tile))
                    return segment;
            }
        }

        throw new InvalidOperationException($"No segment covers {tile.X},{tile.Y}.");
    }

    private static void FillChest(InventorySystem inv, ContainerId chest)
    {
        for (int n = 0; n < 32; n++)
        {
            var stack = MailStack.Single(MailKinds.Letter, new AddressId(1, 1, (byte)n, 0), new MailId(2000 + (uint)n));
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(chest, stack)));
        }
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

    private static Fixture Loaded(int planks, int iron, int logs = 0)
    {
        var catalog = new EndpointCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        var intake = inv.CreateContainer(ContainerSpec.Intake);
        if (logs > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, logs))));
        if (planks > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, planks))));
        if (iron > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, iron))));
        var mail = new MailRegistry();
        var destinations = new Destinations(mail);
        var house = new DestinationId(1);
        Assert.True(destinations.Register(new Destination(house, DestinationType.HouseMailbox, Oak)));
        var endpoints = new BeltEndpoints();
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            PlacementField.Flat(8, 6, 200),
            inv,
            bag,
            Ids());
        return new Fixture(registry, inv, bag, intake, mail, destinations, house, new Wallet(), endpoints);
    }

    private static BuildingDef[] LoadBuildings() =>
        BuildingCatalog.LoadDir(Path.Combine(FindContentRoot(), BuildingCatalog.RelativeDir));

    private static RecipeDef[] LoadRecipes() =>
        RecipeCatalog.LoadDir(Path.Combine(FindContentRoot(), RecipeCatalog.RelativeDir));

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["log"] = LogId,
        ["plank"] = PlankId,
        ["iron_ingot"] = IronId
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
        ContainerId Intake,
        MailRegistry Mail,
        Destinations Destinations,
        DestinationId House,
        Wallet Wallet,
        BeltEndpoints Endpoints)
    {
        public MailId RegisterLetter(AddressId address)
        {
            var id = Mail.Allocate();
            Assert.True(Mail.Register(new MailItem(id, MailKinds.Letter, address, MailKinds.LetterBaseValue, 1, 1)));
            return id;
        }

        public MailId RegisterCargo(AddressId address)
        {
            var id = Mail.Allocate();
            Assert.True(Mail.Register(new MailItem(id, MailKinds.Cargo, address, MailKinds.CargoBaseValue, 1, 2)));
            return id;
        }
    }

    private sealed class EndpointCatalog : IStackCatalog
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
