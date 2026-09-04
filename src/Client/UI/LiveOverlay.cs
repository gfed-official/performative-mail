using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Client.UI;

public static class LiveOverlay
{
    public static bool TryFrom(InventorySystem inventory, out OverlayReplica replica)
    {
        if (inventory is null) throw new ArgumentNullException(nameof(inventory));

        GridContainer? hotbar = null;
        GridContainer? bag = null;
        GridContainer? external = null;
        foreach (var container in inventory.Containers)
        {
            var shape = container.Spec.Shape;
            if (shape.Cols == 8 && shape.Rows == 1)
                hotbar = container;
            else if (shape.Cols == 8 && shape.Rows == 2 && bag is null)
                bag = container;
            else if (shape.Cols == 20 && shape.Rows == 16)
                external = container;
        }

        if (hotbar is null || bag is null)
        {
            replica = default;
            return false;
        }

        replica = new OverlayReplica(hotbar, bag, null, external, new HashSet<EntryId>());
        return true;
    }
}
