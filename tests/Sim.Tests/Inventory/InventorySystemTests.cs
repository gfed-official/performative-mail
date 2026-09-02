using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Tests.Inventory;

public sealed class InventorySystemTests
{
    private static readonly AddressId Oak = new(1, 4, 13, 0);
    private static readonly AddressId Elm = new(1, 5, 2, 0);

    [Fact]
    public void Move_BetweenOpenContainers_CommitsBothDeltas()
    {
        var world = new InventoryHarness();
        var letter = world.Letter(Oak, 3);
        Assert.True(world.Deposit(world.Chest, letter));
        var entry = world.FirstEntry(world.Chest);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Move(world.Chest, entry, world.InvA, Placement.Origin));

        var accepted = Assert.IsType<Accepted>(result);
        Assert.Equal(2, accepted.Deltas.Count);
        Assert.Equal(1u, accepted.Deltas[0].BeforeVersion.Value);
        Assert.Equal(2u, accepted.Deltas[0].Version.Value);
        Assert.True(world.Inv.TryGetContainer(world.InvA, out var invA));
        Assert.NotEqual(EntryId.None, invA.EntryAt(new Cell(0, 0)));
        Assert.Equal(EntryId.None, world.ChestGrid.EntryAt(new Cell(0, 0)));
        Assert.Null(world.Inv.CheckInvariants());
    }

    [Fact]
    public void Move_OccupiedTarget_RejectsWithoutVersionBump()
    {
        var world = new InventoryHarness();
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 1)));
        Assert.True(world.Deposit(world.Chest, world.Letter(Elm, 1)));
        var oak = world.EntryAt(world.Chest, 0, 0);
        var version = world.Version(world.Chest);
        var hash = world.ChestGrid.Hash;

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Move(world.Chest, oak, world.Chest, new Placement(1, 0, false)));

        Assert.Equal(RejectReason.Occupied, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(version, world.Version(world.Chest));
        Assert.Equal(hash, world.ChestGrid.Hash);
    }

    [Fact]
    public void Move_UnknownEntry_RejectsWithoutVersionBump()
    {
        var world = new InventoryHarness();
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 1)));
        var version = world.Version(world.Chest);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Move(world.Chest, new EntryId(99), world.InvA, Placement.Origin));

        Assert.Equal(RejectReason.UnknownEntry, Assert.IsType<Rejected>(result).Reason);
        Assert.Equal(version, world.Version(world.Chest));
    }

    [Fact]
    public void Move_OntoOwnCells_IsIdempotentNoBump()
    {
        var world = new InventoryHarness();
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 1)));
        var entry = world.FirstEntry(world.Chest);
        var version = world.Version(world.Chest);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Move(world.Chest, entry, world.Chest, Placement.Origin));

        var accepted = Assert.IsType<Accepted>(result);
        Assert.Empty(accepted.Deltas);
        Assert.Equal(version, world.Version(world.Chest));
    }

    [Fact]
    public void Apply_NotOpen_Rejects()
    {
        var world = new InventoryHarness(openChestForB: false);
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 1)));
        var entry = world.FirstEntry(world.Chest);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerB),
            new QuickMove(world.Chest, entry, world.InvB));

        Assert.Equal(RejectReason.NotOpen, Assert.IsType<Rejected>(result).Reason);
    }

    [Fact]
    public void Open_ReplacesPreviousExternal()
    {
        var world = new InventoryHarness();
        var other = world.Inv.CreateContainer(ContainerSpec.Chest);
        Assert.IsType<Accepted>(world.Inv.Open(world.PlayerA, other));
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 1)));
        var entry = world.FirstEntry(world.Chest);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new QuickMove(world.Chest, entry, world.InvA));

        Assert.Equal(RejectReason.NotOpen, Assert.IsType<Rejected>(result).Reason);
        world.Inv.Close(world.PlayerA, other);
        Assert.False(world.Inv.IsOpen(world.PlayerA, other));
    }

    [Fact]
    public void Deposit_FromPlayer_IsForbidden()
    {
        var world = new InventoryHarness();
        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Deposit(world.InvA, world.Letter(Oak, 1)));
        Assert.Equal(RejectReason.Forbidden, Assert.IsType<Rejected>(result).Reason);
    }

    [Fact]
    public void Withdraw_ReturnsStack_AndRemovesIt()
    {
        var world = new InventoryHarness();
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 3)));
        var entry = world.FirstEntry(world.Chest);
        var before = InventoryAudit.Of(world.Inv);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Withdraw(world.Chest, entry, Amount.Of(1)));

        var accepted = Assert.IsType<Accepted>(result);
        Assert.NotNull(accepted.Withdrawn);
        Assert.Equal(1, accepted.Withdrawn!.Count);
        Assert.Equal(before.Minus(accepted.Withdrawn), InventoryAudit.Of(world.Inv));
        Assert.Equal(2, world.ChestGrid.Entries.First().Stack.Count);
    }

    [Fact]
    public void Sort_ByAddress_MergesSplitStacks()
    {
        var world = new InventoryHarness();
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 4)));
        var source = world.FirstEntry(world.Chest);
        Assert.IsType<Accepted>(world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Move(world.Chest, source, world.Chest, new Placement(3, 0, false), Amount.Of(1))));
        source = world.EntryAt(world.Chest, 0, 0);
        Assert.IsType<Accepted>(world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Move(world.Chest, source, world.Chest, new Placement(5, 1, false), Amount.Of(1))));
        Assert.True(world.ChestGrid.Entries.Count >= 2);

        var result = world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new Sort(world.Chest, SortKey.ByAddress));

        Assert.IsType<Accepted>(result);
        Assert.Single(world.ChestGrid.Entries);
        var packed = world.ChestGrid.Entries.Single();
        Assert.Equal(4, packed.Stack.Count);
        Assert.Equal(packed.Id, world.ChestGrid.EntryAt(new Cell(0, 0)));
        Assert.Null(world.Inv.CheckInvariants());
    }

    [Fact]
    public void ApplyDelta_ReplicaMatchesAuthoritativeHash()
    {
        var world = new InventoryHarness();
        Assert.True(world.Deposit(world.Chest, world.Letter(Oak, 2)));
        var replica = new InventorySystem(world.Catalog);
        foreach (var container in world.Inv.Containers)
            Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(world.Inv.Snapshot(container.Id)));

        var entry = world.FirstEntry(world.Chest);
        var accepted = Assert.IsType<Accepted>(world.Inv.Apply(
            Actor.Player(world.PlayerA),
            new QuickMove(world.Chest, entry, world.InvA, Amount.Of(1))));
        foreach (var delta in accepted.Deltas)
            Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(delta));

        Assert.True(replica.TryGetContainer(world.Chest, out var replicaChest));
        Assert.Equal(world.ChestGrid.Hash, replicaChest.Hash);
        Assert.Equal(world.ChestGrid.Version, replicaChest.Version);
    }

    [Fact]
    public void ViewersOf_IncludesOwnerAndExternal()
    {
        var world = new InventoryHarness();
        var viewers = world.Inv.ViewersOf(world.Chest).ToArray();
        Assert.Contains(world.PlayerA, viewers);
        Assert.Contains(world.PlayerB, viewers);
        Assert.Contains(world.PlayerA, world.Inv.ViewersOf(world.InvA).ToArray());
    }
}

internal sealed class InventoryHarness
{
    private uint _nextMail = 1;

    public InventoryHarness(bool openChestForB = true)
    {
        Catalog = TestStackCatalog.Default;
        Inv = new InventorySystem(Catalog);
        PlayerA = EntityId.FromClassAndCounter(1, 1);
        PlayerB = EntityId.FromClassAndCounter(1, 2);
        Chest = Inv.CreateContainer(ContainerSpec.Chest);
        InvA = Inv.CreateContainer(ContainerSpec.BaseInventory, PlayerA);
        InvB = Inv.CreateContainer(ContainerSpec.BaseInventory, PlayerB);
        Assert.IsType<Accepted>(Inv.Open(PlayerA, Chest));
        if (openChestForB)
            Assert.IsType<Accepted>(Inv.Open(PlayerB, Chest));
    }

    public IStackCatalog Catalog { get; }

    public InventorySystem Inv { get; }

    public EntityId PlayerA { get; }

    public EntityId PlayerB { get; }

    public ContainerId Chest { get; }

    public ContainerId InvA { get; }

    public ContainerId InvB { get; }

    public GridContainer ChestGrid
    {
        get
        {
            Assert.True(Inv.TryGetContainer(Chest, out var grid));
            return grid;
        }
    }

    public MailStack Letter(AddressId address, int count)
    {
        var ids = new MailId[count];
        for (int i = 0; i < count; i++)
            ids[i] = new MailId(_nextMail++);
        return new MailStack(TestStackCatalog.Letter, address, ids);
    }

    public MailStack Package(AddressId address)
        => MailStack.Single(TestStackCatalog.SmallPackage, address, new MailId(_nextMail++));

    public bool Deposit(ContainerId to, Stack stack)
        => Inv.Apply(Actor.System, new Deposit(to, stack)) is Accepted;

    public EntryId FirstEntry(ContainerId container)
    {
        Assert.True(Inv.TryGetContainer(container, out var grid));
        return grid.Entries.First().Id;
    }

    public EntryId EntryAt(ContainerId container, byte x, byte y)
    {
        Assert.True(Inv.TryGetContainer(container, out var grid));
        return grid.EntryAt(new Cell(x, y));
    }

    public ContainerVersion Version(ContainerId container)
    {
        Assert.True(Inv.TryGetContainer(container, out var grid));
        return grid.Version;
    }
}
