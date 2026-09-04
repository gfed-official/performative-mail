using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Meta;

public static class MetaProfileStore
{
    public const string FileName = "profile.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static MetaProfile Load(string path)
    {
        string json = ContentIds.ReadFile(path);
        var doc = ContentIds.DeserializeObject<ProfileDocument>(json, path);
        if (string.IsNullOrWhiteSpace(doc.ProfileId))
            throw new InvalidOperationException($"{path}: profileId is required.");
        if (string.IsNullOrWhiteSpace(doc.DisplayName))
            throw new InvalidOperationException($"{path}: displayName is required.");
        if (doc.RankXp < 0)
            throw new InvalidOperationException($"{path}: rankXp must be >= 0.");
        if (doc.Unlocks is null)
            throw new InvalidOperationException($"{path}: unlocks is required.");

        return new MetaProfile(
            doc.ProfileId,
            doc.DisplayName,
            doc.RankXp,
            new ProfileUnlocks(
                ReadIds(doc.Unlocks.Kits, path, "unlocks.kits"),
                ReadIds(doc.Unlocks.Archetypes, path, "unlocks.archetypes"),
                ReadStampTiers(doc.Unlocks.StampTiers, path),
                ReadIds(doc.Unlocks.PerkPools, path, "unlocks.perkPools"),
                ReadIds(doc.Unlocks.Cosmetics, path, "unlocks.cosmetics")));
    }

    public static void Save(string path, MetaProfile profile)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var doc = new ProfileDocument
        {
            ProfileId = profile.ProfileId,
            DisplayName = profile.DisplayName,
            RankXp = profile.RankXp,
            Unlocks = new UnlocksDocument
            {
                Kits = ToArray(profile.Unlocks.Kits),
                Archetypes = ToArray(profile.Unlocks.Archetypes),
                StampTiers = profile.Unlocks.StampTiers,
                PerkPools = ToArray(profile.Unlocks.PerkPools),
                Cosmetics = ToArray(profile.Unlocks.Cosmetics)
            }
        };

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(doc, WriteOptions));
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not write '{path}'.", ex);
        }
    }

    public static MetaProfile WriteResults(string path, MetaProfile profile, ResultsPayload results)
    {
        var next = profile.Award(results.PostalRankXp);
        Save(path, next);
        return next;
    }

    private static string[] ReadIds(string[]? values, string path, string field)
    {
        if (values is null)
            return Array.Empty<string>();

        var copy = new string[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] is null || !ContentIds.IsContentId(values[i]))
                throw new InvalidOperationException($"{path}: {field}[{i}] must be a lowercase snake_case content id.");
            copy[i] = values[i];
        }

        return copy;
    }

    private static int ReadStampTiers(int stampTiers, string path)
    {
        if (stampTiers < 0)
            throw new InvalidOperationException($"{path}: unlocks.stampTiers must be >= 0.");
        return stampTiers;
    }

    private static string[] ToArray(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return Array.Empty<string>();

        var copy = new string[values.Count];
        for (int i = 0; i < values.Count; i++)
            copy[i] = values[i];
        return copy;
    }

    private sealed class ProfileDocument
    {
        public string? ProfileId { get; set; }
        public string? DisplayName { get; set; }
        public int RankXp { get; set; }
        public UnlocksDocument? Unlocks { get; set; }
    }

    private sealed class UnlocksDocument
    {
        public string[]? Kits { get; set; }
        public string[]? Archetypes { get; set; }
        public int StampTiers { get; set; }
        public string[]? PerkPools { get; set; }
        public string[]? Cosmetics { get; set; }
    }
}
