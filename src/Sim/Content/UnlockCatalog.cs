using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PerformativeMail.Sim.Content;

public enum UnlockKind
{
    Kit,
    Archetype,
    PerkPool,
    StampTier,
    Reroll
}

public readonly record struct UnlockGrant(UnlockKind Kind, string? Id, int? Value);

public sealed class RankUnlock
{
    public RankUnlock(int rank, UnlockGrant[] grants)
    {
        Rank = rank;
        Grants = grants;
    }

    public int Rank { get; }

    public UnlockGrant[] Grants { get; }
}

public sealed class UnlockTable
{
    public UnlockTable(RankUnlock[] ranks)
    {
        Ranks = ranks;
    }

    public RankUnlock[] Ranks { get; }
}

public static class UnlockCatalog
{
    public const string RelativePath = "unlocks.json";

    public static UnlockTable LoadFile(string path)
        => Parse(ContentIds.ReadFile(path), path);

    public static UnlockTable Parse(string json, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;
        var doc = ContentIds.DeserializeObject<UnlockDocument>(json, source);
        if (doc.Ranks is null || doc.Ranks.Length == 0)
            throw new InvalidOperationException($"{source}: ranks is required.");

        var seen = new HashSet<int>();
        var ranks = new RankUnlock[doc.Ranks.Length];
        for (int i = 0; i < doc.Ranks.Length; i++)
        {
            var row = doc.Ranks[i];
            if (row is null)
                throw new InvalidOperationException($"{source}: ranks[{i}] is empty.");
            if (row.Rank < 1)
                throw new InvalidOperationException($"{source}: ranks[{i}].rank must be >= 1.");
            if (!seen.Add(row.Rank))
                throw new InvalidOperationException($"{source}: duplicate rank {row.Rank}.");
            ranks[i] = new RankUnlock(row.Rank, ReadGrants(row.Unlocks, source, row.Rank));
        }

        return new UnlockTable(ranks);
    }

    private static UnlockGrant[] ReadGrants(JsonElement unlocks, string source, int rank)
    {
        if (unlocks.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks is required.");
        if (unlocks.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks must be an array.");

        int n = unlocks.GetArrayLength();
        if (n == 0)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks is required.");

        var grants = new UnlockGrant[n];
        int i = 0;
        foreach (var el in unlocks.EnumerateArray())
        {
            grants[i] = ReadGrant(el, source, rank, i);
            i++;
        }

        return grants;
    }

    private static UnlockGrant ReadGrant(JsonElement el, string source, int rank, int index)
    {
        if (el.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks[{index}] must be an object.");

        UnlockGrant? grant = null;
        foreach (var prop in el.EnumerateObject())
        {
            if (grant is not null)
            {
                throw new InvalidOperationException(
                    $"{source}: rank {rank} unlocks[{index}] must have exactly one key.");
            }

            grant = prop.Name switch
            {
                "kit" => new UnlockGrant(UnlockKind.Kit, RequireGrantId(prop.Value, source, rank, index, "kit"), null),
                "archetype" => new UnlockGrant(
                    UnlockKind.Archetype,
                    RequireGrantId(prop.Value, source, rank, index, "archetype"),
                    null),
                "perk_pool" => new UnlockGrant(
                    UnlockKind.PerkPool,
                    RequireGrantId(prop.Value, source, rank, index, "perk_pool"),
                    null),
                "stamp_tier" => new UnlockGrant(UnlockKind.StampTier, null, RequirePositive(prop.Value, source, rank, index, "stamp_tier")),
                "reroll" => new UnlockGrant(UnlockKind.Reroll, null, RequirePositive(prop.Value, source, rank, index, "reroll")),
                _ => throw new InvalidOperationException(
                    $"{source}: rank {rank} unlocks[{index}] unknown unlock '{prop.Name}'.")
            };
        }

        if (grant is null)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks[{index}] must have exactly one key.");
        return grant.Value;
    }

    private static string RequireGrantId(JsonElement value, string source, int rank, int index, string field)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks[{index}].{field} is required.");
        string? id = ContentIds.OptionalContentId(value.GetString(), source);
        if (id is null)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks[{index}].{field} is required.");
        return id;
    }

    private static int RequirePositive(JsonElement value, string source, int rank, int index, string field)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int n) || n < 1)
            throw new InvalidOperationException($"{source}: rank {rank} unlocks[{index}].{field} must be >= 1.");
        return n;
    }

    private sealed class UnlockDocument
    {
        public RankDocument[]? Ranks { get; set; }
    }

    private sealed class RankDocument
    {
        public int Rank { get; set; }
        public JsonElement Unlocks { get; set; }
    }
}
