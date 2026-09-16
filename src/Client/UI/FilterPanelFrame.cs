using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public readonly record struct FilterChip(string Id, string Label, bool Selected);

public readonly record struct FilterPanelFrame(
    bool Open,
    IReadOnlyList<FilterChip> Chips,
    int UnmatchedCount)
{
    public static FilterPanelFrame From(
        bool open,
        IReadOnlyList<StreetRecord> streets,
        IReadOnlyCollection<AddressId> unlocked,
        GridContainer? intake,
        AddressSorter sorter,
        SorterOutput selected = SorterOutput.Left)
    {
        if (streets is null) throw new ArgumentNullException(nameof(streets));
        if (unlocked is null) throw new ArgumentNullException(nameof(unlocked));
        if (sorter is null) throw new ArgumentNullException(nameof(sorter));

        var want = new HashSet<byte>();
        foreach (var address in unlocked)
            want.Add(address.Street);

        var rows = new List<StreetRecord>();
        for (int i = 0; i < streets.Count; i++)
        {
            var street = streets[i];
            if (!want.Contains(street.Id))
                continue;
            rows.Add(street);
        }

        rows.Sort((a, b) => a.Id.CompareTo(b.Id));

        byte? selectedStreet = SelectedStreet(sorter, selected);
        var chips = new FilterChip[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            var street = rows[i];
            chips[i] = new FilterChip(
                street.Id.ToString(),
                street.Name,
                selectedStreet == street.Id);
        }

        return new FilterPanelFrame(open, chips, CountUnmatched(intake, sorter));
    }

    private static byte? SelectedStreet(AddressSorter sorter, SorterOutput selected)
    {
        switch (selected)
        {
            case SorterOutput.Left:
            case SorterOutput.Forward:
            case SorterOutput.Right:
                return sorter.Filter(selected).Street;
            case SorterOutput.Overflow:
            default:
                throw new ArgumentOutOfRangeException(nameof(selected), selected, null);
        }
    }

    private static int CountUnmatched(GridContainer? intake, AddressSorter sorter)
    {
        if (intake is null)
            return 0;

        int unmatched = 0;
        foreach (var entry in intake.Entries)
        {
            if (entry.Stack is not MailStack mail)
                continue;
            var item = new BeltItem(0, 0f, mail.Kind, mail.Address);
            if (sorter.Route(in item) == SorterOutput.Overflow)
                unmatched += mail.Count;
        }

        return unmatched;
    }
}
