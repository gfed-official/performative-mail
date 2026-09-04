using System;
using System.Collections.Generic;

namespace PerformativeMail.Sim.Run;

public readonly record struct StampScore
{
    public StampScore(string id, double scoreMult)
    {
        if (id is null || !IsContentId(id))
            throw new ArgumentException("Stamp id must be a lowercase snake_case content id.", nameof(id));
        if (double.IsNaN(scoreMult) || double.IsInfinity(scoreMult) || scoreMult <= 0)
            throw new ArgumentOutOfRangeException(nameof(scoreMult), scoreMult, null);

        Id = id;
        ScoreMult = scoreMult;
    }

    public string Id { get; }

    public double ScoreMult { get; }

    internal static bool IsContentId(string id)
    {
        if (id.Length == 0) return false;
        if (id[0] is < 'a' or > 'z') return false;
        for (int i = 1; i < id.Length; i++)
        {
            char c = id[i];
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_') continue;
            return false;
        }

        return true;
    }
}

public readonly record struct ResultsPayload
{
    private readonly StampScore[] _stamps;

    private ResultsPayload(
        bool victory,
        int shiftsCompleted,
        int deliveries,
        int score,
        int postalRankXp,
        string seedString,
        StampScore[] stamps)
    {
        Victory = victory;
        ShiftsCompleted = shiftsCompleted;
        Deliveries = deliveries;
        Score = score;
        PostalRankXp = postalRankXp;
        SeedString = seedString;
        _stamps = stamps;
    }

    public bool Victory { get; }

    public int ShiftsCompleted { get; }

    public int Deliveries { get; }

    public int Score { get; }

    public int PostalRankXp { get; }

    public string SeedString { get; }

    public IReadOnlyList<StampScore> Stamps => _stamps ?? Array.Empty<StampScore>();

    public static ResultsPayload From(
        bool victory,
        int shiftsCompleted,
        int deliveries,
        int totalEarnedCents,
        string archetype,
        uint seed,
        IReadOnlyList<StampScore> stamps)
    {
        if (archetype is null || !StampScore.IsContentId(archetype))
            throw new ArgumentException("Archetype must be a lowercase snake_case content id.", nameof(archetype));
        if (stamps is null)
            throw new ArgumentNullException(nameof(stamps));

        var copy = CopyStamps(stamps);
        return new ResultsPayload(
            victory,
            shiftsCompleted,
            deliveries,
            ScoreFrom(totalEarnedCents, copy),
            PostalRankXp.Award(shiftsCompleted, victory, deliveries),
            FormatSeed(archetype, seed, copy),
            copy);
    }

    public bool Equals(ResultsPayload other)
    {
        if (Victory != other.Victory
            || ShiftsCompleted != other.ShiftsCompleted
            || Deliveries != other.Deliveries
            || Score != other.Score
            || PostalRankXp != other.PostalRankXp)
            return false;
        if (!string.Equals(SeedString, other.SeedString, StringComparison.Ordinal))
            return false;

        var left = Stamps;
        var right = other.Stamps;
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Victory);
        hash.Add(ShiftsCompleted);
        hash.Add(Deliveries);
        hash.Add(Score);
        hash.Add(PostalRankXp);
        hash.Add(SeedString);
        var stamps = Stamps;
        for (int i = 0; i < stamps.Count; i++)
            hash.Add(stamps[i]);
        return hash.ToHashCode();
    }

    private static int ScoreFrom(int totalEarnedCents, StampScore[] stamps)
    {
        decimal product = 1m;
        for (int i = 0; i < stamps.Length; i++)
            product *= (decimal)stamps[i].ScoreMult;
        return (int)Math.Round(totalEarnedCents * product, 0, MidpointRounding.AwayFromZero);
    }

    private static string FormatSeed(string archetype, uint seed, StampScore[] stamps)
    {
        string arch = FirstToken(archetype).ToUpperInvariant();
        string hex = seed.ToString("X8");
        if (stamps.Length == 0)
            return "PM1-" + arch + "-" + hex;

        var codes = new string[stamps.Length];
        for (int i = 0; i < stamps.Length; i++)
            codes[i] = StampCode(stamps[i].Id);
        return "PM1-" + arch + "-" + hex + "-" + string.Join(".", codes);
    }

    private static string FirstToken(string id)
    {
        int cut = id.IndexOf('_');
        return cut < 0 ? id : id.Substring(0, cut);
    }

    private static string StampCode(string id)
    {
        int n = 1;
        for (int i = 0; i < id.Length; i++)
        {
            if (id[i] == '_') n++;
        }

        var chars = new char[n];
        chars[0] = char.ToUpperInvariant(id[0]);
        int w = 1;
        for (int i = 1; i < id.Length; i++)
        {
            if (id[i] == '_')
                chars[w++] = char.ToUpperInvariant(id[i + 1]);
        }

        return new string(chars);
    }

    private static StampScore[] CopyStamps(IReadOnlyList<StampScore> stamps)
    {
        if (stamps.Count == 0)
            return Array.Empty<StampScore>();

        var copy = new StampScore[stamps.Count];
        for (int i = 0; i < stamps.Count; i++)
            copy[i] = stamps[i];
        return copy;
    }
}
