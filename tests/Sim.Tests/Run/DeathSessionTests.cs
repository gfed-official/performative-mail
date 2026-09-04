using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Tests.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class DeathSessionTests
{
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly AddressId Elm = new(1, 5, 2, 0);
    private static readonly TileCoord DeathTile = new(12, 7);
    private static readonly PlayerPose PoPad = new(400, 800, 0, 90);

    [Fact]
    public void RespawnTicks_TenSecondsAtThirtyHz_IsThreeHundred()
    {
        Assert.Equal(30, TickClock.TickHz);
        Assert.Equal(300, TickClock.TicksFromSeconds(10));
        Assert.Equal(300, DeathSession.RespawnTicks);
        Assert.Equal(900, DeathSession.DespawnTicks);
    }

    [Fact]
    public void Die_EmptiesPlayerContainers_AndBagHoldsStacks()
    {
        var fx = new Fixture();
        fx.Put(fx.Hotbar, fx.Letter(Oak, 2));
        fx.Put(fx.Inventory, fx.Letter(Elm, 1));
        var before = InventoryAudit.Of(fx.Inv);

        Assert.True(fx.Death.Die(fx.Player.Id, DeathTile, 0));

        Assert.Empty(fx.Grid(fx.Hotbar).Entries);
        Assert.Empty(fx.Grid(fx.Inventory).Entries);
        Assert.True(fx.Death.IsDead(fx.Player.Id));
        Assert.True(fx.Death.TryGetBag(fx.Player.Id, out var bag));
        Assert.Equal(DeathTile, bag.Tile);
        Assert.Equal(2, fx.Death.StacksIn(bag).Count);
        Assert.Equal(before, InventoryAudit.Of(fx.Inv));
    }

    [Fact]
    public void Die_RespawnTick_IsPlusThreeHundred()
    {
        var fx = new Fixture();
        fx.Put(fx.Inventory, fx.Letter(Oak, 1));

        Assert.True(fx.Death.Die(fx.Player.Id, DeathTile, 40));
        Assert.True(fx.Death.TryGetRespawnTick(fx.Player.Id, out var tick));
        Assert.Equal(340u, tick);
        Assert.True(fx.Death.TryGetBag(fx.Player.Id, out var bag));
        Assert.Equal(940u, bag.DespawnTick);
    }

    [Fact]
    public void AdvanceTo_AtRespawnTick_RevivesAtPo_BagRemains()
    {
        var fx = new Fixture();
        fx.Put(fx.Inventory, fx.Letter(Oak, 1));
        fx.Death.Die(fx.Player.Id, DeathTile, 0);

        fx.Death.AdvanceTo(299);
        Assert.True(fx.Death.IsDead(fx.Player.Id));
        Assert.Equal(0, fx.Player.Xcm);

        fx.Death.AdvanceTo(300);
        Assert.False(fx.Death.IsDead(fx.Player.Id));
        Assert.Equal(PoPad, fx.Player.Pose);
        Assert.True(fx.Death.TryGetBag(fx.Player.Id, out _));
    }

    [Fact]
    public void AdvanceTo_AtDespawnTick_ReturnsStacksToIntake()
    {
        var fx = new Fixture();
        fx.Put(fx.Hotbar, fx.Letter(Oak, 3));
        var before = InventoryAudit.Of(fx.Inv);
        fx.Death.Die(fx.Player.Id, DeathTile, 0);

        fx.Death.AdvanceTo(899);
        Assert.True(fx.Death.TryGetBag(fx.Player.Id, out var bag));
        Assert.True(fx.Inv.TryGetContainer(bag.Container, out _));
        Assert.Empty(fx.Grid(fx.Intake).Entries);

        fx.Death.AdvanceTo(900);
        Assert.False(fx.Death.TryGetBag(fx.Player.Id, out _));
        Assert.False(fx.Inv.TryGetContainer(bag.Container, out _));
        Assert.Single(fx.Grid(fx.Intake).Entries);
        Assert.Equal(3, fx.Grid(fx.Intake).Entries.First().Stack.Count);
        Assert.Equal(before, InventoryAudit.Of(fx.Inv));
    }

    [Fact]
    public void Die_DoesNotChangeRunState()
    {
        var fx = new Fixture();
        var run = new RunState(RunPhase.Delivery, 1, 100);
        fx.Death.Die(fx.Player.Id, DeathTile, 0);
        fx.Death.AdvanceTo(900);
        Assert.Equal(RunPhase.Delivery, run.Phase);
        Assert.Equal(1, run.Shift);
    }

    [Fact]
    public void Die_WhileDead_IsNoOp()
    {
        var fx = new Fixture();
        fx.Put(fx.Inventory, fx.Letter(Oak, 1));
        Assert.True(fx.Death.Die(fx.Player.Id, DeathTile, 0));
        Assert.False(fx.Death.Die(fx.Player.Id, new TileCoord(1, 1), 10));
        Assert.True(fx.Death.TryGetBag(fx.Player.Id, out var first));
        Assert.Equal(DeathTile, first.Tile);
    }

    [Fact]
    public void Die_Overflow_StillHoldsEveryStack()
    {
        var fx = new Fixture();
        for (int i = 0; i < 33; i++)
        {
            var address = new AddressId(1, 1, (byte)i, 0);
            var dest = i < 7 ? fx.Hotbar : i < 23 ? fx.Inventory : fx.Backpack;
            fx.Put(dest, fx.Letter(address, 1));
        }

        var before = InventoryAudit.Of(fx.Inv);
        Assert.True(fx.Death.Die(fx.Player.Id, DeathTile, 0));
        Assert.True(fx.Death.TryGetBag(fx.Player.Id, out var bag));
        Assert.NotNull(bag.Overflow);
        Assert.Equal(33, fx.Death.StacksIn(bag).Count);
        Assert.Empty(fx.Grid(fx.Hotbar).Entries);
        Assert.Empty(fx.Grid(fx.Inventory).Entries);
        Assert.Empty(fx.Grid(fx.Backpack).Entries);
        Assert.Equal(before, InventoryAudit.Of(fx.Inv));
    }

    private sealed class Fixture
    {
        private uint _nextMail = 1;

        public Fixture()
        {
            Catalog = TestStackCatalog.Default;
            Inv = new InventorySystem(Catalog);
            Player = new PlayerBody(EntityId.FromClassAndCounter(EntityClass.Player, 1));
            Intake = Inv.CreateContainer(ContainerSpec.Intake);
            Hotbar = Inv.CreateContainer(ContainerSpec.Hotbar, Player.Id);
            Inventory = Inv.CreateContainer(ContainerSpec.BaseInventory, Player.Id);
            Backpack = Inv.CreateContainer(ContainerSpec.Backpack, Player.Id);
            Death = new DeathSession(Inv, Intake, PoPad);
            Death.Bind(Player, Hotbar, Inventory, Backpack);
        }

        public IStackCatalog Catalog { get; }

        public InventorySystem Inv { get; }

        public PlayerBody Player { get; }

        public ContainerId Intake { get; }

        public ContainerId Hotbar { get; }

        public ContainerId Inventory { get; }

        public ContainerId Backpack { get; }

        public DeathSession Death { get; }

        public MailStack Letter(AddressId address, int count)
        {
            var ids = new MailId[count];
            for (int i = 0; i < count; i++)
                ids[i] = new MailId(_nextMail++);
            return new MailStack(MailKinds.Letter, address, ids);
        }

        public void Put(ContainerId to, Stack stack)
            => Assert.IsType<Accepted>(Inv.Apply(Actor.System, new Deposit(to, stack)));

        public GridContainer Grid(ContainerId id)
        {
            Assert.True(Inv.TryGetContainer(id, out var grid));
            return grid;
        }
    }
}
