using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;

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
        Assert.Equal(OverlayCell.ConfirmedOpacity, hands.Opacity);

        OverlayCell mail = frame.Hotbar[1, 0];
        Assert.Equal("1", mail.CountLabel);
        Assert.Equal("13", mail.AddressLabel);
        Assert.Equal("1 13", mail.Text);
        Assert.True(mail.Pending);
        Assert.Equal(OverlayCell.PendingOpacity, mail.Opacity);
        Assert.True(mail.Opacity < OverlayCell.ConfirmedOpacity);
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
    public void MiniAddress_DropsUnitWhenZero()
    {
        Assert.Equal("13", OverlayCell.MiniAddress(new AddressId(1, 1, 13, 0)));
        Assert.Equal("13-2", OverlayCell.MiniAddress(new AddressId(1, 1, 13, 2)));
    }

    private sealed class LetterOnlyCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key) => new(1, 1);

        public int MaxStackOf(StackKey key) => 20;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Mail;
    }
}
