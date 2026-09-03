using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PerformativeMail.Sim.World;

public readonly record struct TownSpec(SettlementSize Size, int Count);

public readonly record struct DistrictUnlockSpec(int FromDistrict, int Max);

public sealed class ArchetypeDef
{
    public ArchetypeDef(
        string id,
        int weight,
        TileCoord sizeTiles,
        TownSpec[] towns,
        int[] districtHouseCounts,
        DistrictUnlockSpec apartmentComplexes,
        DistrictUnlockSpec businessDocks,
        double forestRatioMin,
        double resourceMultiplier,
        int rankRequired)
    {
        Id = id;
        Weight = weight;
        SizeTiles = sizeTiles;
        Towns = towns;
        DistrictHouseCounts = districtHouseCounts;
        ApartmentComplexes = apartmentComplexes;
        BusinessDocks = businessDocks;
        ForestRatioMin = forestRatioMin;
        ResourceMultiplier = resourceMultiplier;
        RankRequired = rankRequired;
    }

    public string Id { get; }

    public int Weight { get; }

    public TileCoord SizeTiles { get; }

    public TownSpec[] Towns { get; }

    public int[] DistrictHouseCounts { get; }

    public DistrictUnlockSpec ApartmentComplexes { get; }

    public DistrictUnlockSpec BusinessDocks { get; }

    public double ForestRatioMin { get; }

    public double ResourceMultiplier { get; }

    public int RankRequired { get; }

    public int DistrictHouseTotal
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < DistrictHouseCounts.Length; i++)
                sum += DistrictHouseCounts[i];
            return sum;
        }
    }
}

public static class ArchetypeCatalog
{
    public const string RelativePath = "world/archetypes.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ArchetypeDef[] LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException($"Could not read '{path}'.", ex);
        }

        return Parse(json, path);
    }

    public static ArchetypeDef[] Parse(string json, string source)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;

        ArchetypeDocument[]? docs;
        try
        {
            docs = JsonSerializer.Deserialize<ArchetypeDocument[]>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source}: invalid JSON. {ex.Message}", ex);
        }

        if (docs is null || docs.Length == 0)
            throw new InvalidOperationException($"{source}: expected a non-empty array of archetype defs.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var defs = new ArchetypeDef[docs.Length];
        for (int i = 0; i < docs.Length; i++)
            defs[i] = Read(docs[i], source, i, seen);

        return defs;
    }

    private static ArchetypeDef Read(ArchetypeDocument? doc, string source, int index, HashSet<string> seen)
    {
        if (doc is null)
            throw new InvalidOperationException($"{source}: archetypes[{index}] is empty.");

        string id = RequireId(doc.Id, source, index);
        if (!seen.Add(id))
            throw new InvalidOperationException($"{source}: duplicate id '{id}'.");

        if (doc.Weight <= 0)
            throw new InvalidOperationException($"{source}: '{id}' weight must be positive.");

        var sizeTiles = ReadCoord(doc.SizeTiles, $"{source}: '{id}' sizeTiles");
        if (sizeTiles.X <= 0 || sizeTiles.Y <= 0)
            throw new InvalidOperationException($"{source}: '{id}' sizeTiles must be positive.");

        var towns = ReadTowns(doc.Towns, source, id);
        var counts = ReadDistrictCounts(doc.DistrictHouseCounts, source, id);
        int sum = 0;
        for (int i = 0; i < counts.Length; i++)
            sum += counts[i];

        var largest = towns[0].Size;
        for (int i = 1; i < towns.Length; i++)
        {
            if ((int)towns[i].Size > (int)largest)
                largest = towns[i].Size;
        }

        var band = SettlementBands.Grown(largest);
        if (!band.Contains(sum))
        {
            throw new InvalidOperationException(
                $"{source}: '{id}' districtHouseCounts sum {sum} is outside the {largest.ToString().ToLowerInvariant()} town band {band.MinHouses}-{band.MaxHouses}.");
        }

        var apartments = ReadUnlock(doc.ApartmentComplexes, source, id, "apartmentComplexes");
        var docks = ReadUnlock(doc.BusinessDocks, source, id, "businessDocks");

        if (doc.ForestRatioMin < 0 || doc.ForestRatioMin > 1)
            throw new InvalidOperationException($"{source}: '{id}' forestRatioMin must be between 0 and 1.");
        if (doc.ResourceMultiplier <= 0)
            throw new InvalidOperationException($"{source}: '{id}' resourceMultiplier must be positive.");
        if (doc.RankRequired < 1)
            throw new InvalidOperationException($"{source}: '{id}' rankRequired must be >= 1.");

        return new ArchetypeDef(
            id,
            doc.Weight,
            sizeTiles,
            towns,
            counts,
            apartments,
            docks,
            doc.ForestRatioMin,
            doc.ResourceMultiplier,
            doc.RankRequired);
    }

    private static string RequireId(string? id, string source, int index)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException($"{source}: archetypes[{index}] id is required.");
        id = id.Trim();
        if (!IsContentId(id))
            throw new InvalidOperationException($"{source}: unknown id '{id}'. Ids are lowercase snake_case.");
        return id;
    }

    private static bool IsContentId(string id)
    {
        if (id[0] is < 'a' or > 'z') return false;
        for (int i = 1; i < id.Length; i++)
        {
            char c = id[i];
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_') continue;
            return false;
        }

        return true;
    }

    private static TownSpec[] ReadTowns(TownDocument[]? towns, string source, string id)
    {
        if (towns is null || towns.Length == 0)
            throw new InvalidOperationException($"{source}: '{id}' towns is required.");

        var specs = new TownSpec[towns.Length];
        for (int i = 0; i < towns.Length; i++)
        {
            var town = towns[i];
            if (town is null)
                throw new InvalidOperationException($"{source}: '{id}' towns[{i}] is empty.");
            if (!SettlementBands.TryParse(town.Size, out var size))
            {
                string raw = town.Size ?? "";
                throw new InvalidOperationException(
                    $"{source}: '{id}' towns[{i}] has unknown id '{raw}'. Expected small, medium, large, or city.");
            }

            if (town.Count < 1)
                throw new InvalidOperationException($"{source}: '{id}' towns[{i}] count must be >= 1.");
            specs[i] = new TownSpec(size, town.Count);
        }

        return specs;
    }

    private static int[] ReadDistrictCounts(int[]? counts, string source, string id)
    {
        if (counts is null || counts.Length == 0)
            throw new InvalidOperationException($"{source}: '{id}' districtHouseCounts is required.");

        var copy = new int[counts.Length];
        for (int i = 0; i < counts.Length; i++)
        {
            if (counts[i] < 1)
                throw new InvalidOperationException($"{source}: '{id}' districtHouseCounts[{i}] must be positive.");
            copy[i] = counts[i];
        }

        return copy;
    }

    private static DistrictUnlockSpec ReadUnlock(UnlockDocument? doc, string source, string id, string field)
    {
        if (doc is null)
            throw new InvalidOperationException($"{source}: '{id}' {field} is required.");
        if (doc.FromDistrict < 1)
            throw new InvalidOperationException($"{source}: '{id}' {field}.fromDistrict must be >= 1.");
        if (doc.Max < 0)
            throw new InvalidOperationException($"{source}: '{id}' {field}.max must be >= 0.");
        return new DistrictUnlockSpec(doc.FromDistrict, doc.Max);
    }

    private static TileCoord ReadCoord(int[]? value, string field)
    {
        if (value is null || value.Length != 2)
            throw new InvalidOperationException($"{field} must be [x, y].");
        return new TileCoord(value[0], value[1]);
    }

    private sealed class ArchetypeDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; }

        [JsonPropertyName("sizeTiles")]
        public int[]? SizeTiles { get; set; }

        [JsonPropertyName("towns")]
        public TownDocument[]? Towns { get; set; }

        [JsonPropertyName("districtHouseCounts")]
        public int[]? DistrictHouseCounts { get; set; }

        [JsonPropertyName("apartmentComplexes")]
        public UnlockDocument? ApartmentComplexes { get; set; }

        [JsonPropertyName("businessDocks")]
        public UnlockDocument? BusinessDocks { get; set; }

        [JsonPropertyName("forestRatioMin")]
        public double ForestRatioMin { get; set; }

        [JsonPropertyName("resourceMultiplier")]
        public double ResourceMultiplier { get; set; }

        [JsonPropertyName("rankRequired")]
        public int RankRequired { get; set; }
    }

    private sealed class TownDocument
    {
        [JsonPropertyName("size")]
        public string? Size { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private sealed class UnlockDocument
    {
        [JsonPropertyName("fromDistrict")]
        public int FromDistrict { get; set; }

        [JsonPropertyName("max")]
        public int Max { get; set; }
    }
}
