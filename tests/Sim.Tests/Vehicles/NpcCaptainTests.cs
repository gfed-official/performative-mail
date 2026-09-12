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

public sealed class NpcCaptainTests
{
    private const int TileCm = 200;
    private static readonly EntityId FarPort = EntityId.FromClassAndCounter(EntityClass.Construct, 9);

    [Fact]
    public void Hire_Debits150_AndSeatsCaptainOnParkedMotorboat()
    {
        var fx = WaterWorld();
        var wallet = new Wallet(new Cents(200));

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(wallet, fx.Site, fx.Boat, TileCm));

        Assert.Equal(new Cents(NpcDriver.HireCents), hired.Paid);
        Assert.Equal(new Cents(50), wallet.Balance);
        Assert.Same(hired.Driver, fx.Site.Driver);
        Assert.True(fx.Boat.NpcInDriverSeat);
        Assert.Equal(NpcRoutePhase.Hired, hired.Driver.Phase);
    }

    [Fact]
    public void Hire_RowboatAtPort_RejectedCannotOperate()
    {
        var vehicles = new VehicleTable();
        var site = new SmallPortSite(
            EntityId.FromClassAndCounter(EntityClass.Construct, 3),
            new TileCoord(2, 2),
            Facing.East);
        var rowboat = vehicles.SpawnRowboat(TileCenter(site.ParkingZone));
        Assert.True(rowboat.IsParked);
        Assert.Equal(site.ParkingZone, rowboat.Tile(TileCm));
        var wallet = new Wallet(new Cents(200));

        var rejected = Assert.IsType<NpcHireRejected>(NpcDriver.TryHire(wallet, site, rowboat, TileCm));

        Assert.Equal(NpcHireReject.CannotOperate, rejected.Reason);
        Assert.Equal(new Cents(200), wallet.Balance);
        Assert.Null(site.Driver);
    }

    [Fact]
    public void Hire_MotorboatOffBerth_Rejected()
    {
        var fx = WaterWorld();
        fx.Boat.SetKinematics(TileCenter(new TileCoord(8, 3)), 0f);
        var wallet = new Wallet(new Cents(200));

        var rejected = Assert.IsType<NpcHireRejected>(NpcDriver.TryHire(wallet, fx.Site, fx.Boat, TileCm));

        Assert.Equal(NpcHireReject.NotParked, rejected.Reason);
        Assert.Null(fx.Site.Driver);
    }

    [Fact]
    public void NpcOnWater_OneTick_IsSixtyPercentOfPlayerMotorboat()
    {
        var player = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.MotorboatOnWater);
        var npc = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.MotorboatOnWater.AtRatio(NpcDriver.SpeedRatio));

        Assert.Equal(
            VehicleContext.MotorboatWaterMetersPerSecond * NpcDriver.SpeedRatio,
            VehicleContext.MotorboatOnWater.AtRatio(NpcDriver.SpeedRatio).MaxSpeedMetersPerSecond);
        Assert.Equal(PlayerPose.QuantizeCm(9.0 / TickClock.TickHz), player.Ycm);
        Assert.Equal(PlayerPose.QuantizeCm(9.0 * NpcDriver.SpeedRatio / TickClock.TickHz), npc.Ycm);
        Assert.True(npc.Ycm < player.Ycm);
    }

    [Fact]
    public void Captain_FollowsWaterRoute_AndReturnsToPort()
    {
        var fx = WaterWorld();
        fx.Site.Route.ReplaceStops(new[] { RouteStop.ForConstruct(FarPort) });

        var hired = Assert.IsType<NpcHired>(NpcDriver.TryHire(fx.Wallet, fx.Site, fx.Boat, TileCm));
        Assert.True(hired.Driver.TryBegin(fx.Graph, fx.Anchors));
        Assert.Equal(NpcRoutePhase.Driving, hired.Driver.Phase);

        bool leftBerth = false;
        for (int i = 0; i < 4000; i++)
        {
            hired.Driver.Step();
            if (!SmallPort.HoldsParked(fx.Site.Origin, fx.Site.Facing, fx.Boat, TileCm))
                leftBerth = true;
            if (hired.Driver.Phase == NpcRoutePhase.Done)
                break;
        }

        Assert.True(leftBerth);
        Assert.Equal(NpcRoutePhase.Done, hired.Driver.Phase);
        Assert.True(SmallPort.HoldsParked(fx.Site.Origin, fx.Site.Facing, fx.Boat, TileCm));
        Assert.Equal(fx.Site.ParkingZone, fx.Boat.Tile(TileCm));
        Assert.True(fx.Boat.IsParked);
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);

    private static Fixture WaterWorld()
    {
        var inventory = new InventorySystem(TestStackCatalog.Default);
        var vehicles = new VehicleTable();
        var origin = new TileCoord(2, 2);
        var site = new SmallPortSite(
            EntityId.FromClassAndCounter(EntityClass.Construct, 3),
            origin,
            Facing.East);
        var boat = vehicles.SpawnMotorboat(TileCenter(site.ParkingZone), inventory);
        Assert.True(SmallPort.HoldsParked(origin, Facing.East, boat, TileCm));

        var anchors = new RouteAnchors();
        anchors.AddConstruct(FarPort, new TileCoord(10, 3));
        return new Fixture(
            site,
            boat,
            inventory,
            new Wallet(new Cents(200)),
            anchors,
            WaterGraph());
    }

    private static PlayerPose TileCenter(TileCoord tile) =>
        PlayerPose.FromMeters((tile.X + 0.5) * (TileCm / 100.0), (tile.Y + 0.5) * (TileCm / 100.0), 0, 16384);

    private static RoutingGraph WaterGraph()
    {
        var nodes = new[]
        {
            new RouteNodeRecord(0, new TileCoord(4, 3)),
            new RouteNodeRecord(1, new TileCoord(7, 3)),
            new RouteNodeRecord(2, new TileCoord(10, 3))
        };
        var edges = new[]
        {
            new RouteEdgeRecord(0, 1, 3, WaterNavmesh.SurfaceWater),
            new RouteEdgeRecord(1, 2, 3, WaterNavmesh.SurfaceWater)
        };
        return new RoutingGraph(nodes, edges);
    }

    private sealed class Fixture
    {
        public Fixture(
            SmallPortSite site,
            VehicleBody boat,
            InventorySystem inventory,
            Wallet wallet,
            RouteAnchors anchors,
            RoutingGraph graph)
        {
            Site = site;
            Boat = boat;
            Inventory = inventory;
            Wallet = wallet;
            Anchors = anchors;
            Graph = graph;
        }

        public SmallPortSite Site { get; }

        public VehicleBody Boat { get; }

        public InventorySystem Inventory { get; }

        public Wallet Wallet { get; }

        public RouteAnchors Anchors { get; }

        public RoutingGraph Graph { get; }
    }
}
