using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public sealed class DestinationTypeDef
{
    public DestinationTypeDef(
        string id,
        double insertRate,
        double manualInsertHold,
        int maxAutomatedFeeders,
        bool requiresUnit,
        bool requiresVehicleZone)
    {
        Id = id;
        InsertRate = insertRate;
        ManualInsertHold = manualInsertHold;
        MaxAutomatedFeeders = maxAutomatedFeeders;
        RequiresUnit = requiresUnit;
        RequiresVehicleZone = requiresVehicleZone;
    }

    public string Id { get; }

    public double InsertRate { get; }

    public double ManualInsertHold { get; }

    public int MaxAutomatedFeeders { get; }

    public bool RequiresUnit { get; }

    public bool RequiresVehicleZone { get; }
}

public static class DestinationTypeCatalog
{
    public const string RelativePath = "mail/destinations.json";

    public static DestinationTypeDef[] LoadFile(string path)
        => Parse(ContentIds.ReadFile(path), path);

    public static DestinationTypeDef[] Parse(string json, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;
        var docs = ContentIds.ReadDocuments(json, source);
        if (docs.Length == 0)
            throw new InvalidOperationException($"{source}: expected a non-empty array of destination type defs.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new DestinationTypeDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<DestDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static DestinationTypeDef Read(DestDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        return new DestinationTypeDef(
            id,
            ContentIds.RequireFiniteNonNegative(doc.InsertRate, source, id, "insertRate"),
            ContentIds.RequireFiniteNonNegative(doc.ManualInsertHold, source, id, "manualInsertHold"),
            ContentIds.RequireNonNegative(doc.MaxAutomatedFeeders, source, id, "maxAutomatedFeeders"),
            doc.RequiresUnit,
            doc.RequiresVehicleZone);
    }

    private sealed class DestDocument
    {
        public string? Id { get; set; }
        public double InsertRate { get; set; }
        public double ManualInsertHold { get; set; }
        public int MaxAutomatedFeeders { get; set; }
        public bool RequiresUnit { get; set; }
        public bool RequiresVehicleZone { get; set; }
    }
}
