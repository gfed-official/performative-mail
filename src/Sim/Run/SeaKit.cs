using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Run;

public static class SeaKit
{
    public const string Id = "sea";
    public const string BlueprintId = "bp_rowboat";

    public static void Grant(ISet<string> blueprints)
    {
        if (blueprints is null) throw new ArgumentNullException(nameof(blueprints));
        blueprints.Add(BlueprintId);
    }

    public static void Grant(ShopSession shop)
    {
        if (shop is null) throw new ArgumentNullException(nameof(shop));
        shop.GrantBlueprint(BlueprintId);
    }

    public static bool OwnsRowboatBlueprint(IReadOnlyCollection<string> blueprints)
    {
        if (blueprints is null) throw new ArgumentNullException(nameof(blueprints));
        foreach (var id in blueprints)
        {
            if (string.Equals(id, BlueprintId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
