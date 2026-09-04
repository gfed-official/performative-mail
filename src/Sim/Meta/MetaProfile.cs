using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;

namespace PerformativeMail.Sim.Meta;

public readonly record struct ProfileUnlocks
{
    private readonly string[] _kits;
    private readonly string[] _archetypes;
    private readonly string[] _perkPools;
    private readonly string[] _cosmetics;

    public ProfileUnlocks(
        IReadOnlyList<string> kits,
        IReadOnlyList<string> archetypes,
        int stampTiers,
        IReadOnlyList<string> perkPools,
        IReadOnlyList<string> cosmetics)
    {
        if (stampTiers < 0)
            throw new ArgumentOutOfRangeException(nameof(stampTiers), stampTiers, null);

        _kits = CopyIds(kits, nameof(kits));
        _archetypes = CopyIds(archetypes, nameof(archetypes));
        StampTiers = stampTiers;
        _perkPools = CopyIds(perkPools, nameof(perkPools));
        _cosmetics = CopyIds(cosmetics, nameof(cosmetics));
    }

    public IReadOnlyList<string> Kits => _kits ?? Array.Empty<string>();

    public IReadOnlyList<string> Archetypes => _archetypes ?? Array.Empty<string>();

    public int StampTiers { get; }

    public IReadOnlyList<string> PerkPools => _perkPools ?? Array.Empty<string>();

    public IReadOnlyList<string> Cosmetics => _cosmetics ?? Array.Empty<string>();

    public static ProfileUnlocks RankOne() => new(
        new[] { "land" },
        new[] { "small_island" },
        1,
        new[] { "base" },
        Array.Empty<string>());

    public bool Equals(ProfileUnlocks other)
    {
        if (StampTiers != other.StampTiers)
            return false;
        return Same(Kits, other.Kits)
            && Same(Archetypes, other.Archetypes)
            && Same(PerkPools, other.PerkPools)
            && Same(Cosmetics, other.Cosmetics);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(StampTiers);
        AddAll(hash, Kits);
        AddAll(hash, Archetypes);
        AddAll(hash, PerkPools);
        AddAll(hash, Cosmetics);
        return hash.ToHashCode();
    }

    private static string[] CopyIds(IReadOnlyList<string> ids, string param)
    {
        if (ids is null)
            throw new ArgumentNullException(param);

        if (ids.Count == 0)
            return Array.Empty<string>();

        var copy = new string[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] is null || !ContentIds.IsContentId(ids[i]))
                throw new ArgumentException("Unlock id must be a lowercase snake_case content id.", param);
            copy[i] = ids[i];
        }

        return copy;
    }

    private static bool Same(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static void AddAll(HashCode hash, IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
            hash.Add(values[i]);
    }
}

public readonly record struct MetaProfile
{
    public MetaProfile(string profileId, string displayName, int rankXp, ProfileUnlocks unlocks)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            throw new ArgumentException("Profile id is required.", nameof(profileId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        if (rankXp < 0)
            throw new ArgumentOutOfRangeException(nameof(rankXp), rankXp, null);

        ProfileId = profileId.Trim();
        DisplayName = displayName.Trim();
        RankXp = rankXp;
        Unlocks = unlocks;
    }

    public string ProfileId { get; }

    public string DisplayName { get; }

    public int RankXp { get; }

    public ProfileUnlocks Unlocks { get; }

    public MetaProfile Award(int postalRankXp)
    {
        if (postalRankXp < 0)
            throw new ArgumentOutOfRangeException(nameof(postalRankXp), postalRankXp, null);
        return new MetaProfile(ProfileId, DisplayName, checked(RankXp + postalRankXp), Unlocks);
    }
}
