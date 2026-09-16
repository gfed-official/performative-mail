using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public sealed class FilterPanelState
{
    private readonly AddressFilter[] _filters = new AddressFilter[AddressSorter.FilteredOutputs];

    public bool IsOpen { get; private set; }

    public SorterOutput SelectedOutput { get; private set; } = SorterOutput.Left;

    public bool TryOpen(string defId)
    {
        if (defId is null) throw new ArgumentNullException(nameof(defId));
        if (defId != AddressSorter.BuildingId)
            return false;
        IsOpen = true;
        return true;
    }

    public void Close() => IsOpen = false;

    public void Select(string id)
    {
        if (id is null) throw new ArgumentNullException(nameof(id));
        if (!byte.TryParse(id, out var street))
            return;
        SetSelected(AddressFilter.ForStreet(street));
    }

    public FilterPanelFrame Frame(
        IReadOnlyList<StreetRecord> streets,
        IReadOnlyList<AddressId> unlocked,
        GridContainer? intake)
    {
        var sorter = new AddressSorter(default, default);
        for (int i = 0; i < _filters.Length; i++)
            sorter.SetFilter((SorterOutput)i, _filters[i]);
        return FilterPanelFrame.From(IsOpen, streets, unlocked, intake, sorter, SelectedOutput);
    }

    private void SetSelected(AddressFilter filter)
    {
        switch (SelectedOutput)
        {
            case SorterOutput.Left:
                _filters[0] = filter;
                return;
            case SorterOutput.Forward:
                _filters[1] = filter;
                return;
            case SorterOutput.Right:
                _filters[2] = filter;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(SelectedOutput), SelectedOutput, null);
        }
    }
}
