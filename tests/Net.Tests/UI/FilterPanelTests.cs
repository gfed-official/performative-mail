using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Net.Tests.UI;

public sealed class FilterPanelTests
{
    [Fact]
    public void Frame_UnlockedHouseA_OmitsLockedStreetB()
    {
        var panel = new FilterPanelState();
        Assert.True(panel.TryOpen(AddressSorter.BuildingId));

        var frame = panel.Frame(
            MapBoot.Tables().Streets,
            new[] { MapBoot.HouseA },
            intake: null);

        Assert.True(frame.Open);
        var chip = Assert.Single(frame.Chips);
        Assert.Equal("1", chip.Id);
        Assert.Equal(MapBoot.StreetA, chip.Label);
        Assert.DoesNotContain(frame.Chips, row => row.Label == MapBoot.StreetB);
    }

    [Fact]
    public void Frame_UnmatchedCount_EqualsIntakeOverflowCandidates()
    {
        var catalog = new LetterCatalog();
        var inventory = new InventorySystem(catalog);
        var intakeId = inventory.CreateContainer(ContainerSpec.Intake);
        Assert.IsType<Accepted>(inventory.Apply(
            Actor.System,
            new Deposit(intakeId, MailStack.Single(MailKinds.Letter, MapBoot.HouseA, new MailId(1)))));
        Assert.IsType<Accepted>(inventory.Apply(
            Actor.System,
            new Deposit(intakeId, MailStack.Single(MailKinds.Letter, MapBoot.HouseB, new MailId(2)))));
        var intake = inventory[intakeId];

        var panel = new FilterPanelState();
        Assert.True(panel.TryOpen(AddressSorter.BuildingId));
        panel.Select("1");

        var frame = panel.Frame(
            MapBoot.Tables().Streets,
            new[] { MapBoot.HouseA, MapBoot.HouseB },
            intake);

        Assert.Equal(1, frame.UnmatchedCount);

        var sorter = new AddressSorter(default, default);
        sorter.SetFilter(SorterOutput.Left, AddressFilter.ForStreet(1));
        int overflow = 0;
        foreach (var entry in intake.Entries)
        {
            if (entry.Stack is not MailStack mail)
                continue;
            var item = new BeltItem(0, 0f, mail.Kind, mail.Address);
            if (sorter.Route(item) == SorterOutput.Overflow)
                overflow += mail.Count;
        }

        Assert.Equal(1, overflow);
        Assert.Equal(overflow, frame.UnmatchedCount);
    }

    [Fact]
    public void TryOpen_SorterBuilding_Opens_OtherIdsStayClosed()
    {
        var panel = new FilterPanelState();
        Assert.True(panel.TryOpen(AddressSorter.BuildingId));
        Assert.True(panel.IsOpen);

        panel.Close();
        Assert.False(panel.IsOpen);
        Assert.False(panel.TryOpen("chest"));
        Assert.False(panel.IsOpen);
    }

    private sealed class LetterCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key) => new(1, 1);

        public int MaxStackOf(StackKey key) => 20;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Mail;
    }
}
