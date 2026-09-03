using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public sealed class StampDef
{
    public StampDef(
        string id,
        string name,
        int tier,
        double scoreMult,
        StatModifier[] modifiers,
        RuleFlag[] rules)
    {
        Id = id;
        Name = name;
        Tier = tier;
        ScoreMult = scoreMult;
        Modifiers = modifiers;
        Rules = rules;
    }

    public string Id { get; }

    public string Name { get; }

    public int Tier { get; }

    public double ScoreMult { get; }

    public StatModifier[] Modifiers { get; }

    public RuleFlag[] Rules { get; }
}

public static class StampCatalog
{
    public const string RelativeDir = "stamps";

    public static StampDef[] LoadDir(string dir)
    {
        ContentIds.RequireDirectory(dir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<StampDef>();
        foreach (string path in ContentIds.EnumerateJsonFiles(dir))
            defs.AddRange(Parse(ContentIds.ReadFile(path), path, seen));

        if (defs.Count == 0)
            throw new InvalidOperationException($"{dir}: expected at least one stamp def.");
        return defs.ToArray();
    }

    public static StampDef[] Parse(string json, string source)
        => Parse(json, source, new HashSet<string>(StringComparer.Ordinal));

    private static StampDef[] Parse(string json, string source, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativeDir;
        var docs = ContentIds.ReadDocuments(json, source);
        var defs = new StampDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<StampDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static StampDef Read(StampDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        var modifiers = StatModifiers.Read(doc.Modifiers, source, id);
        var rules = StatModifiers.ReadRules(doc.Rules, source, id);
        if (modifiers.Length == 0 && rules.Length == 0)
            throw new InvalidOperationException($"{source}: '{id}' needs at least one modifier or rule flag.");
        if (doc.Tier < 1)
            throw new InvalidOperationException($"{source}: '{id}' tier must be >= 1.");
        if (double.IsNaN(doc.ScoreMult) || double.IsInfinity(doc.ScoreMult) || doc.ScoreMult <= 0)
            throw new InvalidOperationException($"{source}: '{id}' scoreMult must be a finite number > 0.");

        return new StampDef(
            id,
            ContentIds.RequireName(doc.Name, source, id),
            doc.Tier,
            doc.ScoreMult,
            modifiers,
            rules);
    }

    private sealed class StampDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int Tier { get; set; }
        public double ScoreMult { get; set; }
        public ModifierRow[]? Modifiers { get; set; }
        public string[]? Rules { get; set; }
    }
}
