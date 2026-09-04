using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Run;

public sealed class DraftSession
{
    public const string StreamName = "perks";
    public const int CardCount = 3;
    public const int CommonWeight = 60;
    public const int UncommonWeight = 30;
    public const int RareWeight = 10;
    public const int RarePerShift = 5;

    private readonly PerkDef[] _catalog;
    private readonly uint _seed;

    public DraftSession(IReadOnlyList<PerkDef> catalog, uint seed, int playerCount)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (playerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, null);

        _seed = seed;
        PlayerCount = playerCount;
        _catalog = new PerkDef[catalog.Count];
        for (int i = 0; i < catalog.Count; i++)
            _catalog[i] = catalog[i] ?? throw new ArgumentNullException(nameof(catalog));
        Array.Sort(_catalog, CompareId);
    }

    public int PlayerCount { get; }

    public DraftOffer? Offer { get; private set; }

    public DraftOffer Roll(byte shift)
    {
        if (shift < 1 || shift > RunState.ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, null);
        if (_catalog.Length < CardCount)
            throw new InvalidOperationException($"draft needs at least {CardCount} perks, have {_catalog.Length}.");

        var pool = new List<PerkDef>(_catalog.Length);
        for (int i = 0; i < _catalog.Length; i++)
            pool.Add(_catalog[i]);

        var rng = RngStream.Derive(_seed, StreamName);
        string first = Take(pool, rng, shift);
        string second = Take(pool, rng, shift);
        string third = Take(pool, rng, shift);
        var offer = new DraftOffer(first, second, third);
        Offer = offer;
        return offer;
    }

    internal static int Weight(PerkRarity rarity, byte shift)
    {
        if (shift < 1)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, null);
        return rarity switch
        {
            PerkRarity.Common => CommonWeight,
            PerkRarity.Uncommon => UncommonWeight,
            PerkRarity.Rare => RareWeight + RarePerShift * (shift - 1),
            _ => throw new InvalidOperationException($"unknown perk rarity '{rarity}'.")
        };
    }

    private static string Take(List<PerkDef> pool, RngStream rng, byte shift)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
            total += Weight(pool[i].Rarity, shift);
        if (total <= 0)
            throw new InvalidOperationException("draft pool has no positive rarity weight.");

        int roll = (int)rng.NextBounded((uint)total);
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Weight(pool[i].Rarity, shift);
            if (roll >= 0) continue;
            string id = pool[i].Id;
            pool.RemoveAt(i);
            return id;
        }

        throw new InvalidOperationException("draft walk missed the rolled weight.");
    }

    private static int CompareId(PerkDef a, PerkDef b) => string.CompareOrdinal(a.Id, b.Id);
}
