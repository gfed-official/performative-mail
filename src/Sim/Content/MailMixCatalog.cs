using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Content;

public sealed class MailMixShift
{
    public MailMixShift(int shift, IReadOnlyDictionary<string, double> shares)
    {
        Shift = shift;
        Shares = shares;
    }

    public int Shift { get; }

    public IReadOnlyDictionary<string, double> Shares { get; }
}

public sealed class MailMixDef
{
    public MailMixDef(
        MailMixShift[] shifts,
        double streetStreakRatio,
        double batchIntervalSeconds,
        double batchJitterSeconds,
        double spawnOverhead,
        double distanceMultiplierPerDistrict,
        double shiftMultiplierPerShift,
        double lateValueRatio,
        double misdeliveryPenaltyRatio)
    {
        Shifts = shifts;
        StreetStreakRatio = streetStreakRatio;
        BatchIntervalSeconds = batchIntervalSeconds;
        BatchJitterSeconds = batchJitterSeconds;
        SpawnOverhead = spawnOverhead;
        DistanceMultiplierPerDistrict = distanceMultiplierPerDistrict;
        ShiftMultiplierPerShift = shiftMultiplierPerShift;
        LateValueRatio = lateValueRatio;
        MisdeliveryPenaltyRatio = misdeliveryPenaltyRatio;
    }

    public MailMixShift[] Shifts { get; }

    public double StreetStreakRatio { get; }

    public double BatchIntervalSeconds { get; }

    public double BatchJitterSeconds { get; }

    public double SpawnOverhead { get; }

    public double DistanceMultiplierPerDistrict { get; }

    public double ShiftMultiplierPerShift { get; }

    public double LateValueRatio { get; }

    public double MisdeliveryPenaltyRatio { get; }
}

public static class MailMixCatalog
{
    public const string RelativePath = "mail/mix.json";
    public const int ShiftCount = 5;
    public const double ShareSumTolerance = 0.001;

    public static MailMixDef LoadFile(string path)
        => Parse(ContentIds.ReadFile(path), path);

    public static MailMixDef Parse(string json, string source)
    {
        if (string.IsNullOrWhiteSpace(source)) source = RelativePath;
        var doc = ContentIds.DeserializeObject<MixDocument>(json, source);
        if (doc.Shifts is null || doc.Shifts.Length != ShiftCount)
            throw new InvalidOperationException($"{source}: shifts must list exactly shifts 1-{ShiftCount}.");

        var seen = new HashSet<int>();
        var shifts = new MailMixShift[doc.Shifts.Length];
        for (int i = 0; i < doc.Shifts.Length; i++)
            shifts[i] = ReadShift(doc.Shifts[i], source, i, seen);

        for (int n = 1; n <= ShiftCount; n++)
        {
            if (!seen.Contains(n))
                throw new InvalidOperationException($"{source}: shifts must list exactly shifts 1-{ShiftCount}.");
        }

        return new MailMixDef(
            shifts,
            ContentIds.RequireFiniteNonNegative(doc.StreetStreakRatio, source, "mix", "streetStreakRatio"),
            ContentIds.RequireFiniteNonNegative(doc.BatchIntervalSeconds, source, "mix", "batchIntervalSeconds"),
            ContentIds.RequireFiniteNonNegative(doc.BatchJitterSeconds, source, "mix", "batchJitterSeconds"),
            ContentIds.RequireFiniteNonNegative(doc.SpawnOverhead, source, "mix", "spawnOverhead"),
            ContentIds.RequireFiniteNonNegative(doc.DistanceMultiplierPerDistrict, source, "mix", "distanceMultiplierPerDistrict"),
            ContentIds.RequireFiniteNonNegative(doc.ShiftMultiplierPerShift, source, "mix", "shiftMultiplierPerShift"),
            ContentIds.RequireFiniteNonNegative(doc.LateValueRatio, source, "mix", "lateValueRatio"),
            ContentIds.RequireFiniteNonNegative(doc.MisdeliveryPenaltyRatio, source, "mix", "misdeliveryPenaltyRatio"));
    }

    private static MailMixShift ReadShift(ShiftDocument? doc, string source, int index, HashSet<int> seen)
    {
        if (doc is null)
            throw new InvalidOperationException($"{source}: shifts[{index}] is empty.");
        if (doc.Shift < 1 || doc.Shift > ShiftCount)
            throw new InvalidOperationException($"{source}: shifts[{index}] shift must be 1-{ShiftCount}.");
        if (!seen.Add(doc.Shift))
            throw new InvalidOperationException($"{source}: duplicate shift {doc.Shift}.");
        if (doc.Shares is null)
            throw new InvalidOperationException($"{source}: shift {doc.Shift} shares is required.");

        var shares = new Dictionary<string, double>(StringComparer.Ordinal);
        double sum = 0;
        foreach (var pair in doc.Shares)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new InvalidOperationException($"{source}: shift {doc.Shift} shares has an empty key.");
            string key = pair.Key.Trim();
            if (pair.Value < 0)
                throw new InvalidOperationException($"{source}: shift {doc.Shift} shares '{key}' must be >= 0.");
            if (!shares.TryAdd(key, pair.Value))
                throw new InvalidOperationException($"{source}: shift {doc.Shift} duplicate shares key '{key}'.");
            sum += pair.Value;
        }

        if (Math.Abs(sum - 1.0) > ShareSumTolerance)
            throw new InvalidOperationException($"{source}: shift {doc.Shift} shares must sum to 1.0 ± {ShareSumTolerance} (was {sum}).");

        return new MailMixShift(doc.Shift, shares);
    }

    private sealed class MixDocument
    {
        public ShiftDocument[]? Shifts { get; set; }
        public double StreetStreakRatio { get; set; }
        public double BatchIntervalSeconds { get; set; }
        public double BatchJitterSeconds { get; set; }
        public double SpawnOverhead { get; set; }
        public double DistanceMultiplierPerDistrict { get; set; }
        public double ShiftMultiplierPerShift { get; set; }
        public double LateValueRatio { get; set; }
        public double MisdeliveryPenaltyRatio { get; set; }
    }

    private sealed class ShiftDocument
    {
        public int Shift { get; set; }
        public Dictionary<string, double>? Shares { get; set; }
    }
}
