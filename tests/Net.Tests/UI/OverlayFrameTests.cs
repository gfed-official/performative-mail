using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.UI;

public sealed class OverlayFrameTests
{
    [Fact]
    public void From_BootReplica_HotbarMailShowsCountAddressAndPendingOpacity()
    {
        var replica = OverlayBootReplica.Build();
        var frame = OverlayFrame.From(in replica);

        Assert.Equal(8, frame.Hotbar.Cols);
        Assert.Equal(1, frame.Hotbar.Rows);
        Assert.Equal(8, frame.Inventory.Cols);
        Assert.Equal(2, frame.Inventory.Rows);
        Assert.NotNull(frame.Backpack);
        Assert.Equal(8, frame.Backpack!.Value.Cols);
        Assert.Equal(2, frame.Backpack.Value.Rows);
        Assert.NotNull(frame.External);
        Assert.Equal(8, frame.External!.Value.Cols);
        Assert.Equal(4, frame.External.Value.Rows);

        OverlayCell hands = frame.Hotbar[0, 0];
        Assert.Equal("", hands.Text);
        Assert.False(hands.Pending);
        Assert.Equal((byte)0, hands.District);
        Assert.Equal(OverlayCell.ConfirmedOpacity, hands.Opacity);
        Assert.Equal(OverlayIcon.Hands, hands.Icon);
        Assert.Equal("hands", hands.IconKey);

        OverlayCell mail = frame.Hotbar[1, 0];
        Assert.Equal("1", mail.CountLabel);
        Assert.Equal("1/1/13", mail.AddressLabel);
        Assert.Equal("1 1/1/13", mail.Text);
        Assert.Equal((byte)1, mail.District);
        Assert.True(mail.Pending);
        Assert.Equal(OverlayCell.PendingOpacity, mail.Opacity);
        Assert.True(mail.Opacity < OverlayCell.ConfirmedOpacity);
        Assert.Equal(OverlayIcon.Letter, mail.Icon);
        Assert.Equal("letter", mail.IconKey);

        OverlayCell empty = frame.Hotbar[2, 0];
        Assert.Equal(OverlayIcon.Empty, empty.Icon);
        Assert.Equal("empty", empty.IconKey);
        Assert.Equal((byte)0, empty.District);
    }

    [Fact]
    public void From_OmitsMissingBackpackAndExternal()
    {
        var catalog = new LetterOnlyCatalog();
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbar = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventory = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        var replica = new InventorySystem(catalog);
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(hotbar)));
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(inventory)));

        var frame = OverlayFrame.From(new OverlayReplica(
            replica[hotbar],
            replica[inventory],
            null,
            null,
            new HashSet<EntryId>()));

        Assert.Null(frame.Backpack);
        Assert.Null(frame.External);
    }

    [Fact]
    public void Stamp_SameLiveReplica_IsEqualUntilInventoryChanges()
    {
        var catalog = new LetterOnlyCatalog();
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbar = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventory = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        var replica = new InventorySystem(catalog);
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(hotbar)));
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(inventory)));
        Assert.True(LiveOverlay.TryFrom(replica, out var live));

        var first = live.Stamp();
        Assert.Equal(first, OverlayStamp.From(in live));
        Assert.Same(OverlayReplica.NoPending, live.Pending);

        Assert.IsType<Accepted>(auth.Apply(
            Actor.System,
            new Deposit(hotbar, MailStack.Single(MailKinds.Letter, new AddressId(1, 1, 1, 0), new MailId(1)))));
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(hotbar)));
        Assert.True(LiveOverlay.TryFrom(replica, out var after));
        Assert.NotEqual(first, after.Stamp());
        Assert.Same(OverlayReplica.NoPending, after.Pending);
    }

    [Fact]
    public void MiniAddress_UsesDistrictStreetNumberNotBareHouseNumber()
    {
        Assert.Equal("1/1/13", OverlayCell.MiniAddress(new AddressId(1, 1, 13, 0)));
        Assert.Equal("1/1/13-2", OverlayCell.MiniAddress(new AddressId(1, 1, 13, 2)));
        Assert.Equal("2/4/7", OverlayCell.MiniAddress(new AddressId(2, 4, 7, 0)));
        Assert.NotEqual("13", OverlayCell.MiniAddress(new AddressId(1, 1, 13, 0)));
    }

    [Fact]
    public void From_MailCellKeepsAddressTextAndDestinationDistrict()
    {
        var catalog = new LetterOnlyCatalog();
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbar = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventory = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        var mail = MailStack.Single(MailKinds.Letter, new AddressId(2, 4, 7, 0), new MailId(1));
        Assert.IsType<Accepted>(auth.Apply(Actor.System, new Deposit(hotbar, mail)));

        var replica = new InventorySystem(catalog);
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(hotbar)));
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(inventory)));
        Assert.True(LiveOverlay.TryFrom(replica, out var live));

        OverlayCell cell = OverlayFrame.From(in live).Hotbar[1, 0];
        Assert.Equal("2/4/7", cell.AddressLabel);
        Assert.Equal("1 2/4/7", cell.Text);
        Assert.Equal((byte)2, cell.District);
        Assert.Equal("#E85D3A", DistrictPalette.HexOf(cell.District));
        Assert.Contains("2/4/7", cell.Text);
    }

    [Fact]
    public void From_LiveShapedReplica_HotbarMailUsesHouseNumberNotBootLarch()
    {
        var catalog = new LetterOnlyCatalog();
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbar = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventory = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        var mail = MailStack.Single(MailKinds.Letter, new AddressId(1, 1, 1, 0), new MailId(1));
        Assert.IsType<Accepted>(auth.Apply(Actor.System, new Deposit(hotbar, mail)));

        var replica = new InventorySystem(catalog);
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(hotbar)));
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(inventory)));
        Assert.True(LiveOverlay.TryFrom(replica, out var live));

        var frame = OverlayFrame.From(in live);
        OverlayCell cell = frame.Hotbar[1, 0];
        Assert.Equal("1", cell.CountLabel);
        Assert.Equal("1/1/1", cell.AddressLabel);
        Assert.Equal("1 1/1/1", cell.Text);
        Assert.Equal((byte)1, cell.District);
        Assert.False(cell.Pending);
        Assert.Equal(OverlayCell.ConfirmedOpacity, cell.Opacity);
        Assert.Equal(OverlayIcon.Letter, cell.Icon);
        Assert.Null(frame.Backpack);
        Assert.NotEqual("13", cell.AddressLabel);
        Assert.NotEqual("1 13", cell.Text);
        Assert.NotEqual("1/1/13", cell.AddressLabel);
    }

    [Fact]
    public void From_PackagePostcardAndItem_UseDistinctIcons()
    {
        var catalog = new LetterOnlyCatalog();
        var auth = new InventorySystem(catalog);
        var player = new EntityId(1);
        var hotbar = auth.CreateContainer(ContainerSpec.Hotbar, player);
        var inventory = auth.CreateContainer(ContainerSpec.BaseInventory, player);
        Assert.IsType<Accepted>(auth.Apply(Actor.System, new Deposit(
            hotbar,
            MailStack.Single(MailKinds.Postcard, new AddressId(1, 1, 2, 0), new MailId(1)))));
        Assert.IsType<Accepted>(auth.Apply(Actor.System, new Deposit(
            hotbar,
            MailStack.Single(MailKinds.SmallPackage, new AddressId(1, 1, 3, 0), new MailId(2)))));
        Assert.IsType<Accepted>(auth.Apply(Actor.System, new Deposit(
            inventory,
            new ItemStack(new ItemDefId(1), 4))));

        var replica = new InventorySystem(catalog);
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(hotbar)));
        Assert.Equal(ReplicaResult.Applied, replica.ApplyDelta(auth.Snapshot(inventory)));
        Assert.True(LiveOverlay.TryFrom(replica, out var live));

        var frame = OverlayFrame.From(in live);
        Assert.Equal(OverlayIcon.Hands, frame.Hotbar[0, 0].Icon);
        Assert.Equal(OverlayIcon.Letter, frame.Hotbar[1, 0].Icon);
        Assert.Equal("letter", frame.Hotbar[1, 0].IconKey);
        Assert.Equal((byte)1, frame.Hotbar[1, 0].District);
        Assert.Equal(OverlayIcon.Package, frame.Hotbar[2, 0].Icon);
        Assert.Equal("package", frame.Hotbar[2, 0].IconKey);
        Assert.Equal("1/1/3", frame.Hotbar[2, 0].AddressLabel);
        Assert.Equal((byte)1, frame.Hotbar[2, 0].District);
        Assert.Equal(OverlayIcon.Item, frame.Inventory[0, 0].Icon);
        Assert.Equal("item", frame.Inventory[0, 0].IconKey);
        Assert.Equal("4", frame.Inventory[0, 0].CountLabel);
        Assert.Equal("", frame.Inventory[0, 0].AddressLabel);
        Assert.Equal((byte)0, frame.Inventory[0, 0].District);
    }

    private sealed class LetterOnlyCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key) => new(1, 1);

        public int MaxStackOf(StackKey key) => 20;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Mail;
    }
}
