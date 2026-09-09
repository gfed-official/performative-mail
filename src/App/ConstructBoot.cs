using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class ConstructBoot
{
    public static ConstructRegistry ForWorld(
        ContentBundle bundle,
        ContentIdMap ids,
        WorldTables tables,
        InventorySystem? inventory = null,
        ContainerId from = default)
    {
        if (bundle is null) throw new ArgumentNullException(nameof(bundle));
        if (ids is null) throw new ArgumentNullException(nameof(ids));
        if (tables is null) throw new ArgumentNullException(nameof(tables));

        return new ConstructRegistry(
            bundle.Buildings,
            bundle.Recipes,
            PlacementField.FromWorld(tables),
            inventory,
            from,
            ids.Items);
    }

    public static ConstructRegistry Replica(WorldTables tables)
    {
        var bundle = ContentBoot.Load(out var ids, out _);
        return ForWorld(bundle, ids, tables);
    }
}
