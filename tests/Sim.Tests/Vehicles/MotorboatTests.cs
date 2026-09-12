using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class MotorboatTests
{
    private const int TileCm = 200;
    private static readonly TileCoord PortOrigin = new(2, 1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly ItemDefId StoneId = new(4);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly AddressId Oak = new(1, 4, 13, 0);

    [Fact]
    public void SpawnMotorboat_AllocatesEmptyMailCargoGrid()
    {
        var inventory = new InventorySystem(new MaterialCatalog());
        var vehicles = new VehicleTable();
        var body = vehicles.SpawnMotorboat(PlayerPose.Origin, inventory);

        Assert.Equal(VehicleKind.Motorboat, body.Kind);
        Assert.NotNull(body.Cargo);
        Assert.True(inventory.TryGetContainer(body.Cargo.Value, out var cargo));
        Assert.Empty(cargo.Entries);
        Assert.Equal(8, cargo.Spec.Shape.Cols);
        Assert.Equal(10, cargo.Spec.Shape.Rows);
    }

    [Fact]
    public void MotorboatContext_OnWaterIsNine()
    {
        Assert.Equal(9.0f, VehicleContext.MotorboatOnWater.MaxSpeedMetersPerSecond);
        Assert.Equal(VehicleContext.MotorboatOnWater, VehicleContext.ForKind(VehicleKind.Motorboat));
        Assert.Throws<ArgumentOutOfRangeException>(() => VehicleContext.OnRoadFor(VehicleKind.Motorboat));
        Assert.True(NpcDriver.CanOperate(VehicleKind.Motorboat));
    }

    [Fact]
    public void OnWaterForward_OneTick_IsNineMetresPerSecond()
    {
        var boat = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.MotorboatOnWater);

        Assert.Equal(30, PlayerPose.QuantizeCm(9.0 / 30.0));
        Assert.Equal(0, boat.Xcm);
        Assert.Equal(30, boat.Ycm);
        Assert.Equal(0, boat.Zcm);
    }

    [Fact]
    public void TryBuy_Motorboat_WithoutBlueprint_Rejected()
    {
        var wallet = new Wallet(new Cents(1200));
        var shop = new ShopSession(LoadBoatCatalog(), wallet, seed: 1);
        shop.RollOffers(3);

        var rejected = Assert.IsType<ShopRejected>(shop.TryBuy("motorboat"));

        Assert.Equal(ShopReject.MissingBlueprint, rejected.Reason);
        Assert.Equal(new Cents(1200), wallet.Balance);
    }

    [Fact]
    public void TryBuy_BpMotorboatThenMotorboat_GrantsVehicle()
    {
        var wallet = new Wallet(new Cents(1200));
        var shop = new ShopSession(LoadBoatCatalog(), wallet, seed: 1);
        shop.RollOffers(3);

        var blueprint = Assert.IsType<ShopBought>(shop.TryBuy("bp_motorboat"));
        Assert.Equal("bp_motorboat", blueprint.Blueprint);

        var boat = Assert.IsType<ShopBought>(shop.TryBuy("motorboat"));
        Assert.Equal("motorboat", boat.Vehicle);
        Assert.Equal(new Cents(0), wallet.Balance);
    }

    [Fact]
    public void ParkedMotorboat_PortBelt_LandsInCargo()
    {
        var fx = PortWorld();
        Assert.IsType<Placed>(fx.Registry.TryPlace(SmallPort.BuildingId, PortOrigin, Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(0, 2), Facing.East, Owner));

        var site = new SmallPortSite(
            EntityId.FromClassAndCounter(EntityClass.Construct, 1),
            PortOrigin,
            Facing.East);
        var boat = fx.Vehicles.SpawnMotorboat(TileCenter(site.ParkingZone, 16384), fx.Inv);
        Assert.True(SmallPort.HoldsParked(PortOrigin, Facing.East, boat, TileCm));
        Assert.Equal(new TileCoord(1, 2), site.LoadingFace);

        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var segment = Assert.Single(belts.Segments);
        Assert.Equal(site.LoadingFace, segment.AheadTile);

        var endpoints = new BeltEndpoints();
        endpoints.BindInventory(fx.Inv, fx.Mail);
        endpoints.BindVehicles(fx.Vehicles, TileCm);
        endpoints.BindPorts(new[] { site });

        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        endpoints.Drain(belts, 0, new Destinations(fx.Mail), new Wallet());

        Assert.Empty(segment.Lane(0));
        Assert.True(ContainsMail(fx.Inv, boat.Cargo!.Value, mailId));
    }

    [Fact]
    public void ParkedMotorboat_OffPortBelt_DoesNotLoad()
    {
        var fx = PortWorld();
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(8, 2), Facing.East, Owner));

        var boat = fx.Vehicles.SpawnMotorboat(TileCenter(new TileCoord(10, 2), 16384), fx.Inv);
        Assert.Equal(new TileCoord(9, 2), boat.LoadingFaceTile(TileCm));
        Assert.True(boat.IsParked);
        Assert.False(SmallPort.HoldsParked(PortOrigin, Facing.East, boat, TileCm));

        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var segment = Assert.Single(belts.Segments);
        Assert.Equal(boat.LoadingFaceTile(TileCm), segment.AheadTile);

        var endpoints = new BeltEndpoints();
        endpoints.BindInventory(fx.Inv, fx.Mail);
        endpoints.BindVehicles(fx.Vehicles, TileCm);
        endpoints.BindPorts(Array.Empty<SmallPortSite>());

        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        endpoints.Drain(belts, 0, new Destinations(fx.Mail), new Wallet());

        Assert.Empty(segment.Lane(0));
        Assert.True(fx.Inv.TryGetContainer(boat.Cargo!.Value, out var cargo));
        Assert.Empty(cargo.Entries);
        Assert.False(ContainsMail(fx.Inv, boat.Cargo.Value, mailId));
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);

    private static PlayerPose TileCenter(TileCoord tile, ushort yaw) =>
        PlayerPose.FromMeters((tile.X + 0.5) * (TileCm / 100.0), (tile.Y + 0.5) * (TileCm / 100.0), 0, yaw);

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

    private static Fixture PortWorld()
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(PlankId, 20))));
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(IronId, 10))));
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(StoneId, 10))));
        var field = PlacementField.Flat(12, 8, TileCm)
            .WithHeight(new TileCoord(4, 2), -200)
            .WithHeight(new TileCoord(5, 2), -200);
        var registry = new ConstructRegistry(
            LoadBuildings(),
            LoadRecipes(),
            field,
            inv,
            bag,
            Ids());
        return new Fixture(registry, inv, bag, new MailRegistry(), new VehicleTable());
    }

    private static ShopItemDef[] LoadBoatCatalog()
    {
        string root = Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir);
        string bpPath = Path.Combine(root, "bp_motorboat.json");
        string boatPath = Path.Combine(root, "motorboat.json");
        var bp = ShopCatalog.Parse(File.ReadAllText(bpPath), bpPath);
        var boat = ShopCatalog.Parse(File.ReadAllText(boatPath), boatPath);
        var defs = new ShopItemDef[bp.Length + boat.Length];
        Array.Copy(bp, 0, defs, 0, bp.Length);
        Array.Copy(boat, 0, defs, bp.Length, boat.Length);
        return defs;
    }

    private static BuildingDef[] LoadBuildings() =>
        BuildingCatalog.LoadDir(Path.Combine(FindContentRoot(), BuildingCatalog.RelativeDir));

    private static RecipeDef[] LoadRecipes() =>
        RecipeCatalog.LoadDir(Path.Combine(FindContentRoot(), RecipeCatalog.RelativeDir));

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
        VehicleTable Vehicles)
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
