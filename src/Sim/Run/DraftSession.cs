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
    public const int PersonalCap = 5;

    private readonly PerkDef[] _catalog;
    private readonly Dictionary<string, PerkDef> _byId;
    private readonly uint _seed;
    private readonly HashSet<string> _team = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _greyed = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<uint> _pickedThisOffer = new HashSet<uint>();
    private readonly Dictionary<uint, List<string>> _personal = new Dictionary<uint, List<string>>();
    private readonly List<string> _runPicked = new List<string>();
    private DraftRules _rules = DraftRules.None;
    private byte _shift = 1;

    public DraftSession(IReadOnlyList<PerkDef> catalog, uint seed, int playerCount)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (playerCount < 1)
            throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount, null);

        _seed = seed;
        PlayerCount = playerCount;
        _catalog = new PerkDef[catalog.Count];
        _byId = new Dictionary<string, PerkDef>(catalog.Count, StringComparer.Ordinal);
        for (int i = 0; i < catalog.Count; i++)
        {
            PerkDef def = catalog[i] ?? throw new ArgumentNullException(nameof(catalog));
            _catalog[i] = def;
            if (!_byId.ContainsKey(def.Id))
                _byId.Add(def.Id, def);
        }

        Array.Sort(_catalog, CompareId);
    }

    public int PlayerCount { get; }

    public DraftOffer? Offer { get; private set; }

    public IReadOnlyCollection<string> TeamPicks => _team;

    public bool IsGreyed(string perkId) => perkId != null && _greyed.Contains(perkId);

    public IReadOnlyList<string> PersonalPicks(uint playerId)
    {
        if (!_personal.TryGetValue(playerId, out List<string>? ids) || ids.Count == 0)
            return Array.Empty<string>();
        return ids.ToArray();
    }

    public DraftOffer Roll(byte shift) => Roll(shift, DraftRules.None);

    public DraftOffer Roll(byte shift, DraftRules rules)
    {
        if (shift < 1 || shift > RunState.ShiftCount)
            throw new ArgumentOutOfRangeException(nameof(shift), shift, null);
        if (rules.Rank < 1)
            rules = DraftRules.None;

        _rules = rules;
        _shift = shift;
        _greyed.Clear();
        _pickedThisOffer.Clear();

        var pool = EligiblePool();
        if (pool.Count < CardCount)
            throw new InvalidOperationException($"draft needs at least {CardCount} perks, have {pool.Count}.");

        var rng = RngStream.Derive(_seed, StreamName);
        string first = Take(pool, rng, shift);
        RemoveConflicts(pool, first);
        string second = Take(pool, rng, shift);
        RemoveConflicts(pool, second);
        string third = Take(pool, rng, shift);
        var offer = new DraftOffer(first, second, third);
        Offer = offer;
        return offer;
    }

    public bool TryPick(uint playerId, string perkId, out DraftPickReject reject)
    {
        reject = DraftPickReject.None;
        if (Offer is not DraftOffer offer)
        {
            reject = DraftPickReject.NoOffer;
            return false;
        }

        if (playerId < 1 || playerId > (uint)PlayerCount)
        {
            reject = DraftPickReject.UnknownPlayer;
            return false;
        }

        if (_pickedThisOffer.Contains(playerId))
        {
            reject = DraftPickReject.AlreadyPicked;
            return false;
        }

        if (string.IsNullOrEmpty(perkId) || !InOffer(offer, perkId))
        {
            reject = DraftPickReject.NotInOffer;
            return false;
        }

        if (!_byId.TryGetValue(perkId, out PerkDef? def))
        {
            reject = DraftPickReject.NotInOffer;
            return false;
        }

        if (_greyed.Contains(perkId) || (def.Scope == PerkScope.Team && _team.Contains(perkId)))
        {
            reject = DraftPickReject.Greyed;
            return false;
        }

        if (!PerkGate.MeetsPrerequisites(def, _shift, in _rules))
        {
            reject = DraftPickReject.MissingPrerequisite;
            return false;
        }

        if (PerkGate.ExcludedByRun(def, _runPicked.ToArray(), Lookup))
        {
            reject = DraftPickReject.Excluded;
            return false;
        }

        if (def.Scope == PerkScope.Personal)
        {
            string[] held = PersonalArray(playerId);
            if (held.Length >= PersonalCap)
            {
                reject = DraftPickReject.PersonalCap;
                return false;
            }

            if (PerkGate.CountEqual(held, perkId) >= def.MaxStacks)
            {
                reject = DraftPickReject.MaxStacks;
                return false;
            }
        }

        Accept(playerId, def);
        return true;
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

    private List<PerkDef> EligiblePool()
    {
        string[] taken = _runPicked.ToArray();
        var pool = new List<PerkDef>(_catalog.Length);
        for (int i = 0; i < _catalog.Length; i++)
        {
            PerkDef def = _catalog[i];
            if (def.Scope == PerkScope.Team && _team.Contains(def.Id))
                continue;
            if (!PerkGate.MeetsPrerequisites(def, _shift, in _rules))
                continue;
            if (PerkGate.ExcludedByRun(def, taken, Lookup))
                continue;
            pool.Add(def);
        }

        return pool;
    }

    private void RemoveConflicts(List<PerkDef> pool, string takenId)
    {
        PerkDef taken = Lookup(takenId);
        for (int i = pool.Count - 1; i >= 0; i--)
        {
            if (PerkGate.Conflicts(taken, pool[i]))
                pool.RemoveAt(i);
        }
    }

    private void Accept(uint playerId, PerkDef def)
    {
        _pickedThisOffer.Add(playerId);
        _runPicked.Add(def.Id);
        if (def.Scope == PerkScope.Team)
        {
            _team.Add(def.Id);
            _greyed.Add(def.Id);
            return;
        }

        if (!_personal.TryGetValue(playerId, out List<string>? held))
        {
            held = new List<string>();
            _personal[playerId] = held;
        }

        held.Add(def.Id);
    }

    private PerkDef Lookup(string id) => _byId[id];

    private string[] PersonalArray(uint playerId)
    {
        if (!_personal.TryGetValue(playerId, out List<string>? held) || held.Count == 0)
            return Array.Empty<string>();
        return held.ToArray();
    }

    private static bool InOffer(DraftOffer offer, string perkId)
        => string.Equals(offer.First, perkId, StringComparison.Ordinal)
           || string.Equals(offer.Second, perkId, StringComparison.Ordinal)
           || string.Equals(offer.Third, perkId, StringComparison.Ordinal);

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
