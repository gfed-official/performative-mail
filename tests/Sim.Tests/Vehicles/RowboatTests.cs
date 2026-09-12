using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class RowboatTests
{
    private const int TileCm = 200;
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId RopeId = new(2);

    [Fact]
    public void RepoDefs_SeaKitBlueprint_IsKitOnlyAndNotShopOffered()
    {
        var shop = Index(LoadShop());
        Assert.True(shop.TryGetValue(SeaKit.BlueprintId, out var row));
        Assert.Equal(ShopKind.Blueprint, row.Kind);
        Assert.Equal(0, row.Price);
        Assert.Equal(SeaKit.BlueprintId, row.GrantBlueprint);
        Assert.Contains("kit", row.Tags);
        Assert.Contains("sea", row.Tags);

        var session = new ShopSession(LoadShop(), new Wallet(new Cents(100)), seed: 1);
        session.RollOffers(2);
        Assert.DoesNotContain(session.Offers, offer => offer.Id == SeaKit.BlueprintId);
        Assert.DoesNotContain(session.OwnedBlueprints, id => id == SeaKit.BlueprintId);
    }

    [Fact]
    public void SeaKit_GrantsRowboatBlueprint()
    {
        var session = new ShopSession(LoadShop(), new Wallet(), seed: 1);
        SeaKit.Grant(session);
        Assert.True(SeaKit.OwnsRowboatBlueprint(session.OwnedBlueprints));
        Assert.Contains(SeaKit.BlueprintId, session.OwnedBlueprints);
    }

    [Fact]
    public void Place_ViaSeaKit_ConsumesRecipeAndSpawnsRowboat()
    {
        var fx = Loaded(logs: 8, rope: 2);
        SeaKit.Grant(fx.Blueprints);

        var placed = Assert.IsType<VehiclePlaced>(
            RowboatPlacement.TryPlace(
                fx.Blueprints,
                fx.Inv,
                fx.Bag,
                Ids(),
                fx.Vehicles,
                PlayerPose.Origin));

        Assert.Equal(VehicleKind.Rowboat, placed.Vehicle.Kind);
        Assert.Equal(1, fx.Vehicles.Count);
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, LogId));
        Assert.Equal(0, CountItem(fx.Inv, fx.Bag, RopeId));
        Assert.True(fx.Vehicles.TryGet(placed.Vehicle.Id, out var body));
        Assert.Same(placed.Vehicle, body);
    }

    [Fact]
    public void Place_WithoutSeaKit_Rejected()
    {
        var fx = Loaded(logs: 8, rope: 2);

        var rejected = Assert.IsType<VehiclePlaceRejected>(
            RowboatPlacement.TryPlace(
                fx.Blueprints,
                fx.Inv,
                fx.Bag,
                Ids(),
                fx.Vehicles,
                PlayerPose.Origin));

        Assert.Equal(VehiclePlaceReject.MissingBlueprint, rejected.Reason);
        Assert.Equal(0, fx.Vehicles.Count);
        Assert.Equal(8, CountItem(fx.Inv, fx.Bag, LogId));
        Assert.Equal(2, CountItem(fx.Inv, fx.Bag, RopeId));
    }

    [Fact]
    public void Preview_ViaSeaKit_DoesNotConsume()
    {
        var fx = Loaded(logs: 8, rope: 2);
        SeaKit.Grant(fx.Blueprints);

        Assert.Null(RowboatPlacement.Preview(fx.Blueprints, fx.Inv, fx.Bag, Ids()));
        Assert.Equal(0, fx.Vehicles.Count);
        Assert.Equal(8, CountItem(fx.Inv, fx.Bag, LogId));
        Assert.Equal(2, CountItem(fx.Inv, fx.Bag, RopeId));
    }

    [Fact]
    public void OnWaterForward_OneTick_IsThreeMetresPerSecond()
    {
        var boat = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.RowboatOnWater);

        Assert.Equal(3.0f, VehicleContext.RowboatOnWater.MaxSpeedMetersPerSecond);
        Assert.Equal(10, PlayerPose.QuantizeCm(3.0 / 30.0));
        Assert.Equal(0, boat.Xcm);
        Assert.Equal(10, boat.Ycm);
        Assert.Equal(0, boat.Zcm);
    }

    [Fact]
    public void OnRoadFor_Rowboat_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => VehicleContext.OnRoadFor(VehicleKind.Rowboat));
        Assert.Equal(VehicleContext.RowboatOnWater, VehicleContext.ForKind(VehicleKind.Rowboat));
        Assert.False(NpcDriver.CanOperate(VehicleKind.Rowboat));
        Assert.True(NpcDriver.CanOperate(VehicleKind.MailTruck));
    }

    [Fact]
    public void Hire_Rowboat_RejectedCannotOperate()
    {
        var vehicles = new VehicleTable();
        var boat = vehicles.SpawnRowboat(TileCenter(new TileCoord(6, 6)));
        var site = new VehicleDepotSite(
            EntityId.FromClassAndCounter(EntityClass.Construct, 3),
            new TileCoord(5, 5),
            Facing.East);
        Assert.True(VehicleDepot.HoldsParked(site.Origin, boat, TileCm));
        var wallet = new Wallet(new Cents(200));

        var rejected = Assert.IsType<NpcHireRejected>(NpcDriver.TryHire(wallet, site, boat, TileCm));

        Assert.Equal(NpcHireReject.CannotOperate, rejected.Reason);
        Assert.Equal(new Cents(200), wallet.Balance);
        Assert.Null(site.Driver);
        Assert.False(boat.NpcInDriverSeat);
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);

    private static PlayerPose TileCenter(TileCoord tile) =>
        PlayerPose.FromMeters((tile.X + 0.5) * (TileCm / 100.0), (tile.Y + 0.5) * (TileCm / 100.0), 0, 16384);

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

    private static Fixture Loaded(int logs = 0, int rope = 0)
    {
        var catalog = new MaterialCatalog();
        var inv = new InventorySystem(catalog);
        var bag = inv.CreateContainer(ContainerSpec.Chest);
        if (logs > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(LogId, logs))));
        if (rope > 0)
            Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(bag, new ItemStack(RopeId, rope))));
        return new Fixture(inv, bag, new VehicleTable(), new HashSet<string>(StringComparer.Ordinal));
    }

    private static ShopItemDef[] LoadShop() =>
        ShopCatalog.LoadDir(Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir));

    private static Dictionary<string, ShopItemDef> Index(ShopItemDef[] defs)
    {
        var map = new Dictionary<string, ShopItemDef>(StringComparer.Ordinal);
        foreach (var def in defs)
            map.Add(def.Id, def);
        return map;
    }

    private static Dictionary<string, ItemDefId> Ids() => new(StringComparer.Ordinal)
    {
        ["log"] = LogId,
        ["rope"] = RopeId
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
        InventorySystem Inv,
        ContainerId Bag,
        VehicleTable Vehicles,
        HashSet<string> Blueprints);

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
