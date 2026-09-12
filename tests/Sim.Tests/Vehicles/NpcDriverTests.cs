using PerformativeMail.Sim;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Tests.Inventory;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class NpcDriverTests
{
    private const int TileCm = 200;
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly AddressId Elm = new(1, 5, 2, 0);
    private static readonly AddressId Pine = new(2, 1, 7, 0);
    private static readonly AddressId Stray = new(3, 2, 9, 0);

    [Fact]
    public void Hire_Debits150_AndSeatsNpcOnParkedTruck()
    {
        var fx = RouteWorld();
        var wallet = new Wallet(new Cents(200));

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(wallet, fx.Site, fx.Truck, TileCm));

        Assert.Equal(new Cents(NpcDriver.HireCents), hired.Paid);
        Assert.Equal(new Cents(50), wallet.Balance);
        Assert.Same(hired.Driver, fx.Site.Driver);
        Assert.True(fx.Truck.NpcInDriverSeat);
        Assert.False(fx.Truck.NpcInPassengerSeat);
        Assert.Equal(NpcRoutePhase.Hired, hired.Driver.Phase);
    }

    [Fact]
    public void Hire_SecondDriverOnSameDepot_Rejected()
    {
        var fx = RouteWorld();
        var wallet = new Wallet(new Cents(400));
        Assert.IsType<NpcHired>(NpcDriver.TryHire(wallet, fx.Site, fx.Truck, TileCm));

        var rejected = Assert.IsType<NpcHireRejected>(NpcDriver.TryHire(wallet, fx.Site, fx.Truck, TileCm));

        Assert.Equal(NpcHireReject.AlreadyHired, rejected.Reason);
        Assert.Equal(new Cents(250), wallet.Balance);
    }

    [Fact]
    public void Hire_TruckOffParkingZone_Rejected()
    {
        var fx = RouteWorld();
        fx.Truck.SetKinematics(PlayerPose.FromMeters(9.0, 13.0, 0.0, 16384), 0f);
        var wallet = new Wallet(new Cents(200));

        var rejected = Assert.IsType<NpcHireRejected>(NpcDriver.TryHire(wallet, fx.Site, fx.Truck, TileCm));

        Assert.Equal(NpcHireReject.NotParked, rejected.Reason);
        Assert.Equal(new Cents(200), wallet.Balance);
        Assert.Null(fx.Site.Driver);
    }

    [Fact]
    public void Hire_EmptyWallet_Rejected()
    {
        var fx = RouteWorld();
        var rejected = Assert.IsType<NpcHireRejected>(
            NpcDriver.TryHire(new Wallet(), fx.Site, fx.Truck, TileCm));
        Assert.Equal(NpcHireReject.InsufficientFunds, rejected.Reason);
        Assert.Null(fx.Site.Driver);
        Assert.False(fx.Truck.NpcInDriverSeat);
    }

    [Fact]
    public void NpcOnRoad_OneTick_IsSixtyPercentOfPlayerTruck()
    {
        var player = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.MailTruckOnRoad);
        var npc = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.MailTruckOnRoad.AtRatio(NpcDriver.SpeedRatio));

        Assert.Equal(0.6f, NpcDriver.SpeedRatio);
        Assert.Equal(
            VehicleContext.MailTruckOnRoadMetersPerSecond * NpcDriver.SpeedRatio,
            VehicleContext.MailTruckOnRoad.AtRatio(NpcDriver.SpeedRatio).MaxSpeedMetersPerSecond);
        Assert.Equal(PlayerPose.QuantizeCm(14.0 / TickClock.TickHz), player.Ycm);
        Assert.Equal(PlayerPose.QuantizeCm(14.0 * NpcDriver.SpeedRatio / TickClock.TickHz), npc.Ycm);
        Assert.True(npc.Ycm < player.Ycm);
    }

    [Fact]
    public void ThreeStopRoute_DeliversMatchingMailOnly_MisSortReturnsInTruck()
    {
        var fx = RouteWorld();
        fx.Site.Route.ReplaceStops(new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForDistrict(Elm.District),
            RouteStop.ForAddress(Pine)
        });
        var oak = fx.LoadLetter(Oak);
        var elm = fx.LoadLetter(Elm);
        var pine = fx.LoadLetter(Pine);
        var stray = fx.LoadLetter(Stray);
        var startWallet = fx.Wallet.Balance;

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(fx.Wallet, fx.Site, fx.Truck, TileCm));
        hired.Driver.BindDelivery(fx.Inventory, fx.Destinations, fx.Mailboxes, fx.Wallet, fx.Complaint);
        Assert.True(hired.Driver.TryBegin(fx.Graph, fx.Anchors));

        RunUntilDone(hired.Driver);

        Assert.Equal(NpcRoutePhase.Done, hired.Driver.Phase);
        Assert.True(VehicleDepot.HoldsParked(fx.Site.Origin, fx.Truck, TileCm));
        Assert.False(fx.Mail.Contains(oak));
        Assert.False(fx.Mail.Contains(elm));
        Assert.False(fx.Mail.Contains(pine));
        Assert.True(fx.Mail.Contains(stray));
        Assert.Equal(new[] { stray }, CargoIds(fx));
        Assert.Equal(0, fx.Complaint.Points);
        Assert.Equal(startWallet.Value - NpcDriver.HireCents + 24, fx.Wallet.Balance.Value);
    }

    [Fact]
    public void AddressStop_DoesNotGuessDistrictNeighbor()
    {
        Assert.True(RouteStop.ForAddress(Oak).Accepts(Oak));
        Assert.False(RouteStop.ForAddress(Oak).Accepts(Elm));
        Assert.True(RouteStop.ForDistrict(Elm.District).Accepts(Elm));
        Assert.True(RouteStop.ForDistrict(Elm.District).Accepts(Oak));
        Assert.False(RouteStop.ForDistrict(Elm.District).Accepts(Pine));
        Assert.False(RouteStop.ForConstruct(EntityId.FromClassAndCounter(EntityClass.Construct, 1)).Accepts(Oak));
    }

    [Fact]
    public void EnemyWithin20mOfRoute_AbortsToDepot_WithoutDamage()
    {
        var world = new SimWorld(TestStackCatalog.Default);
        var fx = RouteWorld(world);
        fx.Site.Route.ReplaceStops(new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForDistrict(Elm.District),
            RouteStop.ForAddress(Pine)
        });
        var oak = fx.LoadLetter(Oak);
        var elm = fx.LoadLetter(Elm);
        var pine = fx.LoadLetter(Pine);
        var stray = fx.LoadLetter(Stray);
        var startWallet = fx.Wallet.Balance;
        var enemy = PlantedEnemy.AtMeters(21, 1);
        var rider = world.Players.Spawn(PlayerPose.FromMeters(21, 1, 0, 0));
        Assert.Equal(20, NpcDriver.FleeRangeMetres);

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(fx.Wallet, fx.Site, fx.Truck, TileCm));
        hired.Driver.BindDelivery(fx.Inventory, fx.Destinations, fx.Mailboxes, fx.Wallet, fx.Complaint);
        Assert.True(hired.Driver.TryBegin(fx.Graph, fx.Anchors));
        DriveOffDepot(hired.Driver, fx);
        Assert.Equal(NpcRoutePhase.Driving, hired.Driver.Phase);
        Assert.False(VehicleDepot.HoldsParked(fx.Site.Origin, fx.Truck, TileCm));

        hired.Driver.BindEnemies(new[] { enemy });
        hired.Driver.Step();
        Assert.Equal(NpcRoutePhase.Fleeing, hired.Driver.Phase);

        RunUntilDone(hired.Driver);

        Assert.Equal(NpcRoutePhase.Done, hired.Driver.Phase);
        Assert.True(VehicleDepot.HoldsParked(fx.Site.Origin, fx.Truck, TileCm));
        Assert.True(fx.Truck.IsParked);
        Assert.Equal(new[] { oak, elm, pine, stray }, CargoIds(fx));
        Assert.True(fx.Mail.Contains(oak));
        Assert.True(fx.Mail.Contains(elm));
        Assert.True(fx.Mail.Contains(pine));
        Assert.True(fx.Mail.Contains(stray));
        Assert.Equal(PlantedEnemy.StubHp, enemy.Hp);
        Assert.Equal((byte)100, rider.HpPct);
        Assert.Equal(0, fx.Complaint.Points);
        Assert.Equal(startWallet.Value - NpcDriver.HireCents, fx.Wallet.Balance.Value);
    }

    [Fact]
    public void EnemyBeyond20mOfRoute_DoesNotAbort()
    {
        var fx = RouteWorld();
        fx.Site.Route.ReplaceStops(new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForDistrict(Elm.District),
            RouteStop.ForAddress(Pine)
        });
        var oak = fx.LoadLetter(Oak);
        var elm = fx.LoadLetter(Elm);
        var pine = fx.LoadLetter(Pine);
        var stray = fx.LoadLetter(Stray);
        var enemy = PlantedEnemy.AtMeters(50, 50);

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(fx.Wallet, fx.Site, fx.Truck, TileCm));
        hired.Driver.BindDelivery(fx.Inventory, fx.Destinations, fx.Mailboxes, fx.Wallet, fx.Complaint);
        hired.Driver.BindEnemies(new[] { enemy });
        Assert.True(hired.Driver.TryBegin(fx.Graph, fx.Anchors));

        RunUntilDone(hired.Driver);

        Assert.Equal(NpcRoutePhase.Done, hired.Driver.Phase);
        Assert.True(VehicleDepot.HoldsParked(fx.Site.Origin, fx.Truck, TileCm));
        Assert.False(fx.Mail.Contains(oak));
        Assert.False(fx.Mail.Contains(elm));
        Assert.False(fx.Mail.Contains(pine));
        Assert.True(fx.Mail.Contains(stray));
        Assert.Equal(new[] { stray }, CargoIds(fx));
        Assert.Equal(PlantedEnemy.StubHp, enemy.Hp);
        Assert.Equal(0, fx.Complaint.Points);
    }

    [Fact]
    public void TakeoverAndHandBack_FinishInOneTick()
    {
        var world = new SimWorld(TestStackCatalog.Default);
        var fx = RouteWorld(world);
        fx.Site.Route.ReplaceStops(new[]
        {
            RouteStop.ForAddress(Oak),
            RouteStop.ForAddress(Elm),
            RouteStop.ForAddress(Pine)
        });
        fx.LoadLetter(Oak);
        var rider = world.Players.Spawn(PlayerPose.FromMeters(0, 0, 0, 0));

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(fx.Wallet, fx.Site, fx.Truck, TileCm));
        hired.Driver.BindDelivery(fx.Inventory, fx.Destinations, fx.Mailboxes, fx.Wallet, fx.Complaint);
        Assert.True(hired.Driver.TryBegin(fx.Graph, fx.Anchors));
        for (int i = 0; i < 8; i++)
            hired.Driver.Step();

        var before = fx.Truck.Pose;
        Assert.True(fx.Truck.NpcInDriverSeat);
        Assert.Equal(0u, fx.Truck.Driver.Value);
        Assert.NotEqual(before, rider.Pose);

        Assert.True(world.TryMount(rider.Id, fx.Truck.Id));
        Assert.False(fx.Truck.NpcInDriverSeat);
        Assert.True(fx.Truck.NpcInPassengerSeat);
        Assert.Equal(rider.Id, fx.Truck.Driver);
        Assert.Equal(fx.Truck.Id, rider.VehicleId);
        Assert.Equal(before, fx.Truck.Pose);
        Assert.Equal(before, rider.Pose);

        hired.Driver.Step();
        Assert.Equal(before, fx.Truck.Pose);

        world.ApplyInput(rider.Id, new InputCmd(1, 0, MovementStep.AxisFull, 0, InputButtons.None));
        Assert.Equal(PlayerPose.QuantizeCm(14.0 / TickClock.TickHz), fx.Truck.Pose.Ycm - before.Ycm);

        var taken = fx.Truck.Pose;
        Assert.True(world.TryDismount(rider.Id));
        Assert.True(fx.Truck.NpcInDriverSeat);
        Assert.False(fx.Truck.NpcInPassengerSeat);
        Assert.Equal(0u, fx.Truck.Driver.Value);
        Assert.Equal(0u, rider.VehicleId.Value);
        Assert.Equal(taken, fx.Truck.Pose);
        Assert.False(world.TryDismount(rider.Id));
    }

    private static void RunUntilDone(NpcDriver driver)
    {
        for (int i = 0; i < 4000; i++)
        {
            if (driver.Phase == NpcRoutePhase.Done)
                return;
            driver.Step();
        }

        Assert.Fail("NPC route did not finish.");
    }

    private static void DriveOffDepot(NpcDriver driver, Fixture fx)
    {
        for (int i = 0; i < 40; i++)
            driver.Step();

        Assert.Equal(NpcRoutePhase.Driving, driver.Phase);
        Assert.False(VehicleDepot.HoldsParked(fx.Site.Origin, fx.Truck, TileCm));
        Assert.False(fx.Truck.Tile(TileCm).Equals(fx.Site.ParkingZone));
    }

    private static MailId[] CargoIds(Fixture fx)
    {
        Assert.True(fx.Truck.Cargo.HasValue);
        Assert.True(fx.Inventory.TryGetContainer(fx.Truck.Cargo.Value, out var grid));
        var ids = new List<MailId>();
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is not MailStack mail) continue;
            foreach (var id in mail.Ids)
                ids.Add(id);
        }

        return ids.ToArray();
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);

    private static Fixture RouteWorld(SimWorld? world = null)
    {
        var inventory = world?.Inventory ?? new InventorySystem(TestStackCatalog.Default);
        var vehicles = world?.Vehicles ?? new VehicleTable();
        var park = new TileCoord(6, 6);
        var truck = vehicles.SpawnMailTruck(TileCenter(park), inventory);
        Assert.True(VehicleDepot.HoldsParked(new TileCoord(5, 5), truck, TileCm));

        var site = new VehicleDepotSite(
            EntityId.FromClassAndCounter(EntityClass.Construct, 3),
            new TileCoord(5, 5),
            Facing.East);
        var mail = new MailRegistry();
        var dests = new Destinations(mail);
        var mailboxes = new Dictionary<AddressId, DestinationId>
        {
            [Oak] = new DestinationId(1),
            [Elm] = new DestinationId(2),
            [Pine] = new DestinationId(3),
            [Stray] = new DestinationId(4)
        };
        dests.Register(new Destination(mailboxes[Oak], DestinationType.HouseMailbox, Oak));
        dests.Register(new Destination(mailboxes[Elm], DestinationType.HouseMailbox, Elm));
        dests.Register(new Destination(mailboxes[Pine], DestinationType.HouseMailbox, Pine));
        dests.Register(new Destination(mailboxes[Stray], DestinationType.HouseMailbox, Stray));

        var anchors = new RouteAnchors();
        anchors.AddAddress(Oak, new TileCoord(10, 0));
        anchors.AddDistrict(Elm.District, new TileCoord(2, 0));
        anchors.AddAddress(Elm, new TileCoord(2, 0));
        anchors.AddAddress(Pine, new TileCoord(6, 0));
        return new Fixture(
            site,
            truck,
            inventory,
            mail,
            dests,
            mailboxes,
            new Wallet(new Cents(200)),
            new ComplaintMeter(),
            anchors,
            LineGraph());
    }

    private static PlayerPose TileCenter(TileCoord tile) =>
        PlayerPose.FromMeters((tile.X + 0.5) * (TileCm / 100.0), (tile.Y + 0.5) * (TileCm / 100.0), 0, 16384);

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

    private sealed class Fixture
    {
        public Fixture(
            VehicleDepotSite site,
            VehicleBody truck,
            InventorySystem inventory,
            MailRegistry mail,
            Destinations destinations,
            Dictionary<AddressId, DestinationId> mailboxes,
            Wallet wallet,
            ComplaintMeter complaint,
            RouteAnchors anchors,
            RoutingGraph graph)
        {
            Site = site;
            Truck = truck;
            Inventory = inventory;
            Mail = mail;
            Destinations = destinations;
            Mailboxes = mailboxes;
            Wallet = wallet;
            Complaint = complaint;
            Anchors = anchors;
            Graph = graph;
        }

        public VehicleDepotSite Site { get; }

        public VehicleBody Truck { get; }

        public InventorySystem Inventory { get; }

        public MailRegistry Mail { get; }

        public Destinations Destinations { get; }

        public Dictionary<AddressId, DestinationId> Mailboxes { get; }

        public Wallet Wallet { get; }

        public ComplaintMeter Complaint { get; }

        public RouteAnchors Anchors { get; }

        public RoutingGraph Graph { get; }

        public MailId LoadLetter(AddressId address)
        {
            var id = new MailId((uint)(Mail.Count + 1));
            Assert.True(Mail.Register(new MailItem(id, MailKinds.Letter, address, MailKinds.LetterBaseValue, 1, 1)));
            Assert.True(Truck.Cargo is ContainerId);
            Assert.IsType<Accepted>(Inventory.Apply(
                Actor.System,
                new Deposit(Truck.Cargo.Value, MailStack.Single(MailKinds.Letter, address, id))));
            return id;
        }
    }
}
