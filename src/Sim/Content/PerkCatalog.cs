using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public enum PerkCategory
{
    Carrier,
    Facility,
    Postal,
    Defense
}

public enum PerkScope
{
    Team,
    Personal
}

public enum PerkRarity
{
    Common,
    Uncommon,
    Rare
}

public sealed class PerkPrerequisites
{
    public PerkPrerequisites(string[] builtAny, int? shiftMin, int? rankMin)
    {
        BuiltAny = builtAny;
        ShiftMin = shiftMin;
        RankMin = rankMin;
    }

    public string[] BuiltAny { get; }

    public int? ShiftMin { get; }

    public int? RankMin { get; }
}

public sealed class PerkDef
{
    public PerkDef(
        string id,
        string name,
        string description,
        PerkCategory category,
        PerkScope scope,
        PerkRarity rarity,
        StatModifier[] modifiers,
        string[] unlockRecipes,
        RuleFlag[] rules,
        PerkPrerequisites? prerequisites,
        string[] excludes,
        int maxStacks,
        string[] tags)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Scope = scope;
        Rarity = rarity;
        Modifiers = modifiers;
        UnlockRecipes = unlockRecipes;
        Rules = rules;
        Prerequisites = prerequisites;
        Excludes = excludes;
        MaxStacks = maxStacks;
        Tags = tags;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public PerkCategory Category { get; }

    public PerkScope Scope { get; }

    public PerkRarity Rarity { get; }

    public StatModifier[] Modifiers { get; }

    public string[] UnlockRecipes { get; }

    public RuleFlag[] Rules { get; }

    public PerkPrerequisites? Prerequisites { get; }

    public string[] Excludes { get; }

    public int MaxStacks { get; }

    public string[] Tags { get; }
}

public static class PerkCatalog
{
    public const string RelativeDir = "perks";

    public static PerkDef[] LoadDir(string dir)
    {
        ContentIds.RequireDirectory(dir);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new List<PerkDef>();
        foreach (string path in ContentIds.EnumerateJsonFiles(dir))
            defs.AddRange(Parse(ContentIds.ReadFile(path), path, seen));

        if (defs.Count == 0)
            throw new InvalidOperationException($"{dir}: expected at least one perk def.");
        return defs.ToArray();
    }

    public static PerkDef[] Parse(string json, string source)
        => Parse(json, source, new HashSet<string>(StringComparer.Ordinal));

    private static PerkDef[] Parse(string json, string source, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativeDir;
        var docs = ContentIds.ReadDocuments(json, source);
        var defs = new PerkDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(ContentIds.Deserialize<PerkDocument>(docs[i], source, i), source, i, seen);
        return defs;
    }

    private static PerkDef Read(PerkDocument doc, string source, int index, HashSet<string> seen)
    {
        string id = ContentIds.RequireId(doc.Id, source, index);
        ContentIds.AddUnique(seen, id, source);
        var modifiers = StatModifiers.Read(doc.Modifiers, source, id);
        var rules = StatModifiers.ReadRules(doc.Rules, source, id);
        if (modifiers.Length == 0 && rules.Length == 0)
            throw new InvalidOperationException($"{source}: '{id}' needs at least one modifier or rule flag.");

        int maxStacks = doc.MaxStacks ?? 1;
        if (maxStacks < 1)
            throw new InvalidOperationException($"{source}: '{id}' maxStacks must be >= 1.");

        return new PerkDef(
            id,
            ContentIds.RequireName(doc.Name, source, id),
            RequireDescription(doc.Description, source, id),
            ParseCategory(doc.Category, source, id),
            ParseScope(doc.Scope, source, id),
            ParseRarity(doc.Rarity, source, id),
            modifiers,
            ReadUnlockRecipes(doc.Unlocks, source, id),
            rules,
            ReadPrerequisites(doc.Prerequisites, source, id),
            ContentIds.ReadIdList(doc.Excludes, source, id, "excludes", required: false),
            maxStacks,
            ContentIds.ReadTags(doc.Tags, source, id));
    }

    private static string RequireDescription(string? description, string source, string id)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException($"{source}: '{id}' description is required.");
        return description.Trim();
    }

    private static PerkCategory ParseCategory(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' category", "carrier", "facility", "postal", "defense");
        return token switch
        {
            "carrier" => PerkCategory.Carrier,
            "facility" => PerkCategory.Facility,
            "postal" => PerkCategory.Postal,
            "defense" => PerkCategory.Defense,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown category '{raw}'.")
        };
    }

    private static PerkScope ParseScope(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' scope", "team", "personal");
        return token == "team" ? PerkScope.Team : PerkScope.Personal;
    }

    private static PerkRarity ParseRarity(string? raw, string source, string id)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' rarity", "common", "uncommon", "rare");
        return token switch
        {
            "common" => PerkRarity.Common,
            "uncommon" => PerkRarity.Uncommon,
            "rare" => PerkRarity.Rare,
            _ => throw new InvalidOperationException($"{source}: '{id}' unknown rarity '{raw}'.")
        };
    }

    private static string[] ReadUnlockRecipes(UnlockDocument[]? unlocks, string source, string id)
    {
        if (unlocks is null || unlocks.Length == 0) return Array.Empty<string>();

        var recipes = new string[unlocks.Length];
        for (int i = 0; i < unlocks.Length; i++)
        {
            var row = unlocks[i];
            if (row is null)
                throw new InvalidOperationException($"{source}: '{id}' unlocks[{i}] is empty.");
            string? recipe = ContentIds.OptionalContentId(row.Recipe, source);
            if (recipe is null)
                throw new InvalidOperationException($"{source}: '{id}' unlocks[{i}].recipe is required.");
            recipes[i] = recipe;
        }

        return recipes;
    }

    private static PerkPrerequisites? ReadPrerequisites(PrereqDocument? doc, string source, string id)
    {
        if (doc is null) return null;

        int? shiftMin = null;
        if (doc.ShiftMin is int shift)
        {
            if (shift < 1)
                throw new InvalidOperationException($"{source}: '{id}' prerequisites.shiftMin must be >= 1.");
            shiftMin = shift;
        }

        int? rankMin = null;
        if (doc.RankMin is int rank)
        {
            if (rank < 1)
                throw new InvalidOperationException($"{source}: '{id}' prerequisites.rankMin must be >= 1.");
            rankMin = rank;
        }

        return new PerkPrerequisites(
            ContentIds.ReadIdList(doc.BuiltAny, source, id, "prerequisites.builtAny", required: false),
            shiftMin,
            rankMin);
    }

    private sealed class PerkDocument
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Scope { get; set; }
        public string? Rarity { get; set; }
        public ModifierRow[]? Modifiers { get; set; }
        public UnlockDocument[]? Unlocks { get; set; }
        public string[]? Rules { get; set; }
        public PrereqDocument? Prerequisites { get; set; }
        public string[]? Excludes { get; set; }
        public int? MaxStacks { get; set; }
        public string[]? Tags { get; set; }
    }

    private sealed class UnlockDocument
    {
        public string? Recipe { get; set; }
    }

    private sealed class PrereqDocument
    {
        public string[]? BuiltAny { get; set; }
        public int? ShiftMin { get; set; }
        public int? RankMin { get; set; }
    }
}
