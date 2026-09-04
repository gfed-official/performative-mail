using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PerformativeMail.Sim.World;

public sealed class BalanceTable
{
    public BalanceTable(
        int[] baseQuota,
        double playerScaleExponent,
        double spawnOverhead,
        int[] prepSeconds,
        int[] deliverySeconds,
        int paydaySeconds,
        int draftSeconds,
        double complaintDecayPerSecond,
        int complaintInspectorThreshold,
        int respawnSeconds,
        int deathBagDespawnSeconds,
        int worldItemDespawnSeconds,
        int interestRadius,
        int playerHp,
        double playerRegen,
        double walkSpeed,
        double sprintSpeed,
        double weightSpeedFloor,
        double npcSpeedRatio,
        double operatedBeltMult,
        double salvageRatioDelivery,
        int rerollsPerRun,
        int rankXpPerRank)
    {
        BaseQuota = baseQuota;
        PlayerScaleExponent = playerScaleExponent;
        SpawnOverhead = spawnOverhead;
        PrepSeconds = prepSeconds;
        DeliverySeconds = deliverySeconds;
        PaydaySeconds = paydaySeconds;
        DraftSeconds = draftSeconds;
        ComplaintDecayPerSecond = complaintDecayPerSecond;
        ComplaintInspectorThreshold = complaintInspectorThreshold;
        RespawnSeconds = respawnSeconds;
        DeathBagDespawnSeconds = deathBagDespawnSeconds;
        WorldItemDespawnSeconds = worldItemDespawnSeconds;
        InterestRadius = interestRadius;
        PlayerHp = playerHp;
        PlayerRegen = playerRegen;
        WalkSpeed = walkSpeed;
        SprintSpeed = sprintSpeed;
        WeightSpeedFloor = weightSpeedFloor;
        NpcSpeedRatio = npcSpeedRatio;
        OperatedBeltMult = operatedBeltMult;
        SalvageRatioDelivery = salvageRatioDelivery;
        RerollsPerRun = rerollsPerRun;
        RankXpPerRank = rankXpPerRank;
    }

    public int[] BaseQuota { get; }

    public double PlayerScaleExponent { get; }

    public double SpawnOverhead { get; }

    public int[] PrepSeconds { get; }

    public int[] DeliverySeconds { get; }

    public int PaydaySeconds { get; }

    public int DraftSeconds { get; }

    public double ComplaintDecayPerSecond { get; }

    public int ComplaintInspectorThreshold { get; }

    public int RespawnSeconds { get; }

    public int DeathBagDespawnSeconds { get; }

    public int WorldItemDespawnSeconds { get; }

    public int InterestRadius { get; }

    public int PlayerHp { get; }

    public double PlayerRegen { get; }

    public double WalkSpeed { get; }

    public double SprintSpeed { get; }

    public double WeightSpeedFloor { get; }

    public double NpcSpeedRatio { get; }

    public double OperatedBeltMult { get; }

    public double SalvageRatioDelivery { get; }

    public int RerollsPerRun { get; }

    public int RankXpPerRank { get; }
}

public static class BalanceCatalog
{
    public const string RelativePath = "balance.json";
    public const int ShiftCount = 5;

    public static BalanceTable LoadFile(string path)
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

    public static BalanceTable Parse(string json, string source)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{source}: invalid JSON. {ex.Message}", ex);
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{source}: expected a flat key/value object.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var prop in root.EnumerateObject())
            keys.Add(prop.Name);

        return new BalanceTable(
            ReadIntArray(root, keys, source, "baseQuota"),
            ReadNumber(root, keys, source, "playerScaleExponent"),
            ReadNumber(root, keys, source, "spawnOverhead"),
            ReadIntArray(root, keys, source, "prepSeconds"),
            ReadIntArray(root, keys, source, "deliverySeconds"),
            ReadInt(root, keys, source, "paydaySeconds"),
            ReadInt(root, keys, source, "draftSeconds"),
            ReadNumber(root, keys, source, "complaintDecayPerSecond"),
            ReadInt(root, keys, source, "complaintInspectorThreshold"),
            ReadInt(root, keys, source, "respawnSeconds"),
            ReadInt(root, keys, source, "deathBagDespawnSeconds"),
            ReadInt(root, keys, source, "worldItemDespawnSeconds"),
            ReadInt(root, keys, source, "interestRadius"),
            ReadInt(root, keys, source, "playerHp"),
            ReadNumber(root, keys, source, "playerRegen"),
            ReadNumber(root, keys, source, "walkSpeed"),
            ReadNumber(root, keys, source, "sprintSpeed"),
            ReadNumber(root, keys, source, "weightSpeedFloor"),
            ReadNumber(root, keys, source, "npcSpeedRatio"),
            ReadNumber(root, keys, source, "operatedBeltMult"),
            ReadNumber(root, keys, source, "salvageRatioDelivery"),
            ReadInt(root, keys, source, "rerollsPerRun"),
            ReadInt(root, keys, source, "rankXpPerRank"));
    }

    private static int[] ReadIntArray(JsonElement root, HashSet<string> keys, string source, string name)
    {
        var el = Require(root, keys, source, name);
        if (el.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"{source}: '{name}' must be an array of {ShiftCount} integers.");
        if (el.GetArrayLength() != ShiftCount)
            throw new InvalidOperationException($"{source}: '{name}' must have {ShiftCount} entries.");

        var values = new int[ShiftCount];
        int i = 0;
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out int n) || n < 0)
                throw new InvalidOperationException($"{source}: '{name}[{i}]' must be a non-negative integer.");
            values[i++] = n;
        }

        return values;
    }

    private static int ReadInt(JsonElement root, HashSet<string> keys, string source, string name)
    {
        var el = Require(root, keys, source, name);
        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out int n) || n < 0)
            throw new InvalidOperationException($"{source}: '{name}' must be a non-negative integer.");
        return n;
    }

    private static double ReadNumber(JsonElement root, HashSet<string> keys, string source, string name)
    {
        var el = Require(root, keys, source, name);
        if (el.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException($"{source}: '{name}' must be a number.");
        double n = el.GetDouble();
        if (double.IsNaN(n) || double.IsInfinity(n) || n < 0)
            throw new InvalidOperationException($"{source}: '{name}' must be a finite non-negative number.");
        return n;
    }

    private static JsonElement Require(JsonElement root, HashSet<string> keys, string source, string name)
    {
        if (!keys.Contains(name) || !root.TryGetProperty(name, out var el))
            throw new InvalidOperationException($"{source}: missing required key '{name}'.");
        return el;
    }
}
