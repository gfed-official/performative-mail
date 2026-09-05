using System;
using System.Collections.Generic;
using System.Reflection;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Content;

public static class DebugSpawnCoverage
{
    public static void RequireComplete(ContentBundle bundle, DebugSpawnCatalog catalog)
    {
        if (bundle is null) throw new ArgumentNullException(nameof(bundle));
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < catalog.Coverage.Count; i++)
            claimed.Add(catalog.Coverage[i].BundleProperty);

        var missing = new List<string>();
        var properties = typeof(ContentBundle).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        for (int i = 0; i < properties.Length; i++)
        {
            if (!claimed.Contains(properties[i].Name))
                missing.Add(properties[i].Name);
        }

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "Unclaimed ContentBundle properties: " + string.Join(", ", missing));

        var itemRows = new HashSet<string>(StringComparer.Ordinal);
        var mailRows = new HashSet<string>(StringComparer.Ordinal);
        bool bike = false;
        for (int i = 0; i < catalog.Rows.Count; i++)
        {
            var row = catalog.Rows[i];
            switch (row.Kind)
            {
                case DebugSpawnKind.Item:
                    itemRows.Add(row.Id.ContentId);
                    break;
                case DebugSpawnKind.Mail:
                    mailRows.Add(row.Id.ContentId);
                    break;
                case DebugSpawnKind.Bike:
                    bike = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(catalog), row.Kind, null);
            }
        }

        if (!SameSet(itemRows, bundle.Items, d => d.Id))
            throw new InvalidOperationException("Spawn item rows drifted from ContentBundle.Items.");
        if (!SameSet(mailRows, bundle.Kinds, d => d.Id))
            throw new InvalidOperationException("Spawn mail rows drifted from ContentBundle.Kinds.");
        if (!bike)
            throw new InvalidOperationException("Spawn catalog is missing the Bike row.");

        if (DefCount(catalog, DebugFacetPolicyKind.SpawnItems) != itemRows.Count)
            throw new InvalidOperationException("SpawnItems DefCount drifted from item rows.");
        if (DefCount(catalog, DebugFacetPolicyKind.SpawnMail) != mailRows.Count)
            throw new InvalidOperationException("SpawnMail DefCount drifted from mail rows.");
        if (catalog.Rows.Count != itemRows.Count + mailRows.Count + 1)
            throw new InvalidOperationException("Spawn rows drifted from actionable defs.");
    }

    private static int DefCount(DebugSpawnCatalog catalog, DebugFacetPolicyKind policy)
    {
        for (int i = 0; i < catalog.Coverage.Count; i++)
        {
            if (catalog.Coverage[i].Policy == policy)
                return catalog.Coverage[i].DefCount;
        }

        return -1;
    }

    private static bool SameSet<T>(HashSet<string> rows, T[] defs, Func<T, string> idOf)
    {
        if (rows.Count != defs.Length)
            return false;
        for (int i = 0; i < defs.Length; i++)
        {
            if (!rows.Contains(idOf(defs[i])))
                return false;
        }

        return true;
    }
}
