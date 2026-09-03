using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public enum StatOp
{
    Mul,
    Add
}

public readonly record struct StatModifier(Stat Stat, StatOp Op, double Value);

public static class StatModifiers
{
    public static StatModifier[] Read(ModifierRow[]? rows, string source, string id)
    {
        if (rows is null || rows.Length == 0) return Array.Empty<StatModifier>();

        var copy = new StatModifier[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row is null)
                throw new InvalidOperationException($"{source}: '{id}' modifiers[{i}] is empty.");
            copy[i] = new StatModifier(
                ParseStat(row.Stat, source, id, i),
                ParseOp(row.Op, source, id, i),
                ContentIds.RequireFinite(row.Value, source, id, $"modifiers[{i}].value"));
        }

        return copy;
    }

    public static RuleFlag[] ReadRules(string[]? rows, string source, string id)
    {
        if (rows is null || rows.Length == 0) return Array.Empty<RuleFlag>();

        var copy = new RuleFlag[rows.Length];
        var seen = new HashSet<RuleFlag>();
        for (int i = 0; i < rows.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(rows[i]))
                throw new InvalidOperationException($"{source}: '{id}' rules[{i}] is empty.");
            string raw = rows[i].Trim();
            if (!RuleFlags.TryParse(raw, out var flag))
                throw new InvalidOperationException($"{source}: '{id}' unknown rule flag '{raw}'.");
            if (!seen.Add(flag))
                throw new InvalidOperationException($"{source}: '{id}' duplicate rule flag '{raw}'.");
            copy[i] = flag;
        }

        return copy;
    }

    public static Stat ParseStat(string? raw, string source, string id, int index)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"{source}: '{id}' modifiers[{index}].stat is required.");
        raw = raw.Trim();
        if (!Stats.TryParse(raw, out var stat))
            throw new InvalidOperationException($"{source}: '{id}' unknown stat '{raw}'.");
        return stat;
    }

    public static StatOp ParseOp(string? raw, string source, string id, int index)
    {
        string token = ContentIds.RequireClosed(raw, source, $"'{id}' modifiers[{index}].op", "mul", "add");
        return token == "mul" ? StatOp.Mul : StatOp.Add;
    }
}

internal sealed class ModifierRow
{
    public string? Stat { get; set; }
    public string? Op { get; set; }
    public double Value { get; set; }
}
