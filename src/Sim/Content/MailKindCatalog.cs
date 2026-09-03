using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Content;

public sealed class MailKindDef
{
    public MailKindDef(
        string id,
        string name,
        Footprint grid,
        int maxStack,
        int baseValue,
        WeightClass weight,
        bool carryByHand,
        int beltLanes,
        double beltLength,
        int deadlineOffsetShifts,
        string[] acceptedBy,
        int complaintOnMisdelivery,
        int unlockShift)
    {
        Id = id;
        Name = name;
        Grid = grid;
        MaxStack = maxStack;
        BaseValue = baseValue;
        Weight = weight;
        CarryByHand = carryByHand;
        BeltLanes = beltLanes;
        BeltLength = beltLength;
        DeadlineOffsetShifts = deadlineOffsetShifts;
        AcceptedBy = acceptedBy;
        ComplaintOnMisdelivery = complaintOnMisdelivery;
        UnlockShift = unlockShift;
    }

    public string Id { get; }

    public string Name { get; }

    public Footprint Grid { get; }

    public int MaxStack { get; }

    public int BaseValue { get; }

    public WeightClass Weight { get; }

    public bool CarryByHand { get; }

    public int BeltLanes { get; }

    public double BeltLength { get; }

    public int DeadlineOffsetShifts { get; }

    public string[] AcceptedBy { get; }

    public int ComplaintOnMisdelivery { get; }

    public int UnlockShift { get; }
}

public static class MailKindCatalog
{
    public const string RelativePath = "mail/kinds.json";

    public static MailKindDef[] LoadFile(string path)
        => Parse(ContentIds.ReadFile(path), path);

    public static MailKindDef[] Parse(string json, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;
        var docs = ContentIds.ReadDocuments(json, source);
        if (docs.Length == 0)
            throw new InvalidOperationException($"{source}: expected a non-empty array of mail kind defs.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new MailKindDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<KindDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static MailKindDef Read(KindDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        if (doc.BeltLanes is not 1 and not 2)
            throw new InvalidOperationException($"{source}: '{id}' beltLanes must be 1 or 2.");

        string[] acceptedBy = ContentIds.ReadIdList(doc.AcceptedBy, source, id, "acceptedBy", required: true);
        if (acceptedBy.Length == 0)
            throw new InvalidOperationException($"{source}: '{id}' acceptedBy is required.");

        return new MailKindDef(
            id,
            ContentIds.RequireName(doc.Name, source, id),
            ContentIds.RequireGrid(doc.Grid, source, id, "grid"),
            ContentIds.RequireMaxStack(doc.MaxStack, source, id),
            ContentIds.RequireNonNegative(doc.BaseValue, source, id, "baseValue"),
            ContentIds.ParseWeight(doc.WeightClass, source, id),
            doc.CarryByHand,
            doc.BeltLanes,
            ContentIds.RequireFiniteNonNegative(doc.BeltLength, source, id, "beltLength"),
            ContentIds.RequireNonNegative(doc.DeadlineOffsetShifts, source, id, "deadlineOffsetShifts"),
            acceptedBy,
            ContentIds.RequireNonNegative(doc.ComplaintOnMisdelivery, source, id, "complaintOnMisdelivery"),
            ContentIds.RequireUnlockShift(doc.UnlockShift, source, id));
    }

    private sealed class KindDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int[]? Grid { get; set; }
        public int MaxStack { get; set; }
        public int BaseValue { get; set; }
        public string? WeightClass { get; set; }
        public bool CarryByHand { get; set; }
        public int BeltLanes { get; set; }
        public double BeltLength { get; set; }
        public int DeadlineOffsetShifts { get; set; }
        public string[]? AcceptedBy { get; set; }
        public int ComplaintOnMisdelivery { get; set; }
        public int UnlockShift { get; set; }
    }
}
