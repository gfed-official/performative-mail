using System;
using System.Collections.Generic;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Content;

public enum DebugSpawnKind : byte { Item, Mail, Bike }

public readonly record struct DebugSpawnId(DebugSpawnKind Kind, string ContentId);

public readonly record struct DebugSpawnRow(
    DebugSpawnId Id,
    string Label,
    DebugSpawnKind Kind);

public enum DebugFacetPolicyKind : byte { SpawnItems, SpawnMail, SpawnBike, Deferred, NotGrantable }

public readonly record struct DebugFacetCoverage(
    string BundleProperty,
    DebugFacetPolicyKind Policy,
    string? DeferReason,
    int DefCount);

public sealed class DebugSpawnCatalog
{
    public DebugSpawnCatalog(
        IReadOnlyList<DebugSpawnRow> rows,
        IReadOnlyList<DebugFacetCoverage> coverage)
    {
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Coverage = coverage ?? throw new ArgumentNullException(nameof(coverage));
    }

    public IReadOnlyList<DebugSpawnRow> Rows { get; }

    public IReadOnlyList<DebugFacetCoverage> Coverage { get; }

    public static DebugSpawnCatalog From(ContentBundle bundle, ContentIdMap ids)
    {
        if (bundle is null) throw new ArgumentNullException(nameof(bundle));
        if (ids is null) throw new ArgumentNullException(nameof(ids));
        if (ids.Items.Count != bundle.Items.Length)
            throw new InvalidOperationException("ContentIdMap item count drifted from ContentBundle.Items.");

        var items = (ItemDef[])bundle.Items.Clone();
        Array.Sort(items, (a, b) => string.CompareOrdinal(a.Id, b.Id));
        var kinds = (MailKindDef[])bundle.Kinds.Clone();
        Array.Sort(kinds, (a, b) => string.CompareOrdinal(a.Id, b.Id));

        var rows = new List<DebugSpawnRow>(items.Length + kinds.Length + 1);
        for (int i = 0; i < items.Length; i++)
        {
            var def = items[i];
            rows.Add(new DebugSpawnRow(
                new DebugSpawnId(DebugSpawnKind.Item, def.Id),
                def.Name,
                DebugSpawnKind.Item));
        }

        for (int i = 0; i < kinds.Length; i++)
        {
            var def = kinds[i];
            if (!ids.TryMail(def.Id, out _))
                throw new InvalidOperationException($"Unmapped mail kind '{def.Id}'.");
            rows.Add(new DebugSpawnRow(
                new DebugSpawnId(DebugSpawnKind.Mail, def.Id),
                def.Name,
                DebugSpawnKind.Mail));
        }

        string bikeLabel = "Bike";
        for (int i = 0; i < bundle.Shop.Length; i++)
        {
            if (bundle.Shop[i].Id != "bike")
                continue;
            bikeLabel = bundle.Shop[i].Name;
            break;
        }

        rows.Add(new DebugSpawnRow(
            new DebugSpawnId(DebugSpawnKind.Bike, "bike"),
            bikeLabel,
            DebugSpawnKind.Bike));

        return new DebugSpawnCatalog(rows, CoverageOf(bundle));
    }

    private static DebugFacetCoverage[] CoverageOf(ContentBundle bundle) =>
        new[]
        {
            new DebugFacetCoverage(nameof(ContentBundle.Streets), DebugFacetPolicyKind.Deferred, "streets are not grantable", bundle.Streets.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Archetypes), DebugFacetPolicyKind.Deferred, "archetypes are not grantable", bundle.Archetypes.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Balance), DebugFacetPolicyKind.Deferred, "balance is not grantable", 1),
            new DebugFacetCoverage(nameof(ContentBundle.Items), DebugFacetPolicyKind.SpawnItems, null, bundle.Items.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Containers), DebugFacetPolicyKind.Deferred, "containers are not grantable", bundle.Containers.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Kinds), DebugFacetPolicyKind.SpawnMail, null, bundle.Kinds.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Mix), DebugFacetPolicyKind.Deferred, "mail mix is not grantable", 1),
            new DebugFacetCoverage(nameof(ContentBundle.Destinations), DebugFacetPolicyKind.Deferred, "destinations are not grantable", bundle.Destinations.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Buildings), DebugFacetPolicyKind.Deferred, "buildings are not grantable", bundle.Buildings.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Recipes), DebugFacetPolicyKind.Deferred, "recipes are not grantable", bundle.Recipes.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Shop), DebugFacetPolicyKind.Deferred, "non-vehicle shop rows; bike informs the Bike spawn row", bundle.Shop.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Perks), DebugFacetPolicyKind.Deferred, "perks are not grantable", bundle.Perks.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Stamps), DebugFacetPolicyKind.Deferred, "stamps are not grantable", bundle.Stamps.Length),
            new DebugFacetCoverage(nameof(ContentBundle.Unlocks), DebugFacetPolicyKind.Deferred, "unlocks are not grantable", 1)
        };
}
