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

public sealed class VehicleLoadingFaceTests
{
    private const int TileCm = 200;
    private static readonly TileCoord ChestTile = new(1, 1);
    private static readonly TileCoord InserterTile = new(2, 1);
    private static readonly ItemDefId LogId = new(1);
    private static readonly ItemDefId PlankId = new(2);
    private static readonly ItemDefId IronId = new(3);
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly AddressId Oak = new(1, 4, 13, 0);

    [Fact]
    public void ParkedTruck_InserterPush_LandsInCargo()
    {
        var fx = Loaded(planks: 1, iron: 2, logs: 4);
        Assert.IsType<Placed>(fx.Registry.TryPlace("chest", ChestTile, Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(Inserter.BuildingId, InserterTile, Facing.East, Owner));

        var chest = fx.Inv.CreateContainer(ContainerSpec.Chest);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var inserters = new InserterNetwork();
        inserters.BindInventory(fx.Inv, fx.Mail);
        inserters.BindChest(ChestTile, chest);
        inserters.Compile(fx.Registry.All, belts);
        inserters.BindVehicles(fx.Vehicles, TileCm);
        Assert.Single(inserters.Inserters);

        var truck = fx.Vehicles.SpawnMailTruck(PlayerPose.FromMeters(9.0, 3.0, 0.0, 16384), fx.Inv);
        Assert.Equal(new TileCoord(4, 1), truck.Tile(TileCm));
        Assert.Equal(new TileCoord(3, 1), truck.LoadingFaceTile(TileCm));
        Assert.True(truck.IsParked);

        var mailId = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(chest, MailStack.Single(MailKinds.Letter, Oak, mailId))));

        inserters.StepTicks(belts, Inserter.TransferPeriodTicks);

        Assert.False(ContainsMail(fx.Inv, chest, mailId));
        Assert.True(fx.Inv.TryGetContainer(truck.Cargo!.Value, out _));
        Assert.True(ContainsMail(fx.Inv, truck.Cargo.Value, mailId));
    }

    [Fact]
    public void MovingTruck_InserterPush_Rejected()
    {
        var fx = Loaded(planks: 1, iron: 2, logs: 4);

        var truck = fx.Vehicles.SpawnMailTruck(PlayerPose.FromMeters(9.0, 3.0, 0.0, 16384), fx.Inv);
        Assert.Equal(Facing.East, truck.Facing);

        truck.Apply(new InputCmd(0, 0, MovementStep.AxisFull, 16384, InputButtons.None), VehicleContext.MailTruckOnRoad);
        Assert.False(truck.IsParked);
        Assert.Equal(Facing.East, truck.Facing);

        var face = truck.LoadingFaceTile(TileCm);
        var inserterTile = new TileCoord(face.X - 1, face.Y);
        var chestTile = new TileCoord(inserterTile.X - 1, inserterTile.Y);

        Assert.IsType<Placed>(fx.Registry.TryPlace("chest", chestTile, Facing.East, Owner));
        Assert.IsType<Placed>(fx.Registry.TryPlace(Inserter.BuildingId, inserterTile, Facing.East, Owner));

        var chest = fx.Inv.CreateContainer(ContainerSpec.Chest);
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var inserters = new InserterNetwork();
        inserters.BindInventory(fx.Inv, fx.Mail);
        inserters.BindChest(chestTile, chest);
        inserters.Compile(fx.Registry.All, belts);
        inserters.BindVehicles(fx.Vehicles, TileCm);
        Assert.Single(inserters.Inserters);

        var mailId = fx.RegisterLetter(Oak);
        Assert.IsType<Accepted>(fx.Inv.Apply(Actor.System, new Deposit(chest, MailStack.Single(MailKinds.Letter, Oak, mailId))));

        inserters.StepTicks(belts, Inserter.TransferPeriodTicks);

        Assert.True(ContainsMail(fx.Inv, chest, mailId));
        Assert.True(fx.Inv.TryGetContainer(truck.Cargo!.Value, out var cargoGrid));
        Assert.Empty(cargoGrid.Entries);
    }

    [Fact]
    public void ParkedTruck_BeltEndpoint_LandsInCargo()
    {
        var fx = Loaded(planks: 1, iron: 1);
        Assert.IsType<Placed>(fx.Registry.TryPlace(BeltNetwork.BuildingId, new TileCoord(1, 1), Facing.East, Owner));
        var belts = new BeltNetwork();
        belts.Compile(fx.Registry.All);
        var segment = Assert.Single(belts.Segments);

        var truck = fx.Vehicles.SpawnMailTruck(PlayerPose.FromMeters(7.0, 3.0, 0.0, 16384), fx.Inv);
        Assert.Equal(new TileCoord(3, 1), truck.Tile(TileCm));
        Assert.Equal(new TileCoord(2, 1), truck.LoadingFaceTile(TileCm));
        Assert.Equal(segment.AheadTile, truck.LoadingFaceTile(TileCm));

        var endpoints = new BeltEndpoints();
        endpoints.BindInventory(fx.Inv, fx.Mail);
        endpoints.BindVehicles(fx.Vehicles, TileCm);

        var mailId = fx.RegisterLetter(Oak);
        Assert.True(segment.TryInsert(0, (int)mailId.Value, 2.0f));

        belts.StepTicks(1);
        var destinations = new Destinations(fx.Mail);
        var wallet = new Wallet();
        endpoints.Drain(belts, 0, destinations, wallet);

        Assert.Empty(segment.Lane(0));
        Assert.True(fx.Inv.TryGetContainer(truck.Cargo!.Value, out _));
        Assert.True(ContainsMail(fx.Inv, truck.Cargo.Value, mailId));
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

    private static Fixture Loaded(int planks = 0, int iron = 0, int logs = 0)
    {
        var catalog = new VehicleFaceCatalog();
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
            PlacementField.Flat(8, 6, TileCm),
            inv,
            bag,
            Ids());
        return new Fixture(registry, inv, bag, mail, new VehicleTable());
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

    private sealed class VehicleFaceCatalog : IStackCatalog
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
