using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Client.UI;

public readonly record struct OverlayReplica(
    GridContainer Hotbar,
    GridContainer Inventory,
    GridContainer? Backpack,
    GridContainer? External,
    IReadOnlySet<EntryId> Pending);
