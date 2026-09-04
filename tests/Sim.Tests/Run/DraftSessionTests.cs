using System.IO;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class DraftSessionTests
{
    [Fact]
    public void RepoPerks_LoadTwelveAcrossCategories()
    {
        var perks = LoadRepoPerks();
        Assert.Equal(12, perks.Length);
        Assert.Contains(perks, p => p.Category == PerkCategory.Carrier);
        Assert.Contains(perks, p => p.Category == PerkCategory.Facility);
        Assert.Contains(perks, p => p.Category == PerkCategory.Postal);
        Assert.Contains(perks, p => p.Category == PerkCategory.Defense);
        Assert.Contains(perks, p => p.Id == "long_legs");
        Assert.Contains(perks, p => p.Id == "express_lane");
        Assert.Contains(perks, p => p.Id == "insured");
    }

    [Fact]
    public void Roll_RepoPerks_AlwaysThreeLegalDistinctCards()
    {
        var perks = LoadRepoPerks();
        var ids = new HashSet<string>(perks.Select(p => p.Id), StringComparer.Ordinal);
        var draft = new DraftSession(perks, seed: 1, playerCount: 4);
        var offer = draft.Roll(1);

        Assert.Equal(3, DistinctCount(offer));
        Assert.Contains(offer.First, ids);
        Assert.Contains(offer.Second, ids);
        Assert.Contains(offer.Third, ids);
        Assert.Equal(offer, draft.Offer);
    }

    [Fact]
    public void Roll_SameSeedAndPlayerCount_ReproducesOffer()
    {
        var perks = LoadRepoPerks();
        var a = new DraftSession(perks, seed: 0x7F3A9C21, playerCount: 2);
        var b = new DraftSession(perks, seed: 0x7F3A9C21, playerCount: 2);

        Assert.Equal(a.Roll(1), b.Roll(1));
    }

    [Fact]
    public void Roll_GoldenSeed_OffersInsuredQuickHandsUnionRep()
    {
        var draft = new DraftSession(LoadRepoPerks(), seed: 0x7F3A9C21, playerCount: 1);
        var offer = draft.Roll(1, ArcadeWithBelt);
        Assert.Equal("insured", offer.First);
        Assert.Equal("quick_hands", offer.Second);
        Assert.Equal("union_rep", offer.Third);
    }

    [Fact]
    public void Roll_UsesPerksStreamAndShift1Weights()
    {
        var perks = LoadRepoPerks();
        var draft = new DraftSession(perks, seed: 0x7F3A9C21, playerCount: 1);
        var offer = draft.Roll(1, ArcadeWithBelt);
        var expected = Walk(perks, 0x7F3A9C21, shift: 1, common: 60, uncommon: 30, rare: 10);

        Assert.Equal(expected, offer);
        Assert.Equal("perks", DraftSession.StreamName);
    }

    [Fact]
    public void Roll_Shift2_UsesRareWeight15()
    {
        var perks = LoadRepoPerks();
        var draft = new DraftSession(perks, seed: 0x7F3A9C21, playerCount: 1);
        var offer = draft.Roll(2, ArcadeWithBelt);
        var expected = Walk(perks, 0x7F3A9C21, shift: 2, common: 60, uncommon: 30, rare: 15);

        Assert.Equal(expected, offer);
    }

    [Fact]
    public void Roll_CatalogShorterThanThree_Throws()
    {
        var defs = PerkCatalog.Parse(
            """
            {
              "id": "long_legs",
              "name": "Long Legs",
              "description": "Solo.",
              "category": "carrier",
              "scope": "personal",
              "rarity": "common",
              "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.12 } ]
            }
            """,
            "one-perk");
        var draft = new DraftSession(defs, seed: 1, playerCount: 1);
        var ex = Assert.Throws<InvalidOperationException>(() => draft.Roll(1));
        Assert.Contains("3", ex.Message);
    }

    [Fact]
    public void Ctor_PlayerCountZero_Throws()
    {
        var defs = ThreeCardCatalog();
        Assert.Throws<ArgumentOutOfRangeException>(() => new DraftSession(defs, seed: 1, playerCount: 0));
    }

    [Fact]
    public void Roll_UnsortedCatalog_MatchesIdOrder()
    {
        var defs = PerkCatalog.Parse(
            """
            [
              {
                "id": "union_rep",
                "name": "Union Rep",
                "description": "Quota.",
                "category": "postal",
                "scope": "team",
                "rarity": "uncommon",
                "modifiers": [ { "stat": "QuotaMult", "op": "mul", "value": 0.9 } ]
              },
              {
                "id": "big_pockets",
                "name": "Big Pockets",
                "description": "Rows.",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "InventoryRows", "op": "add", "value": 1 } ]
              },
              {
                "id": "express_lane",
                "name": "Express Lane",
                "description": "Belts.",
                "category": "facility",
                "scope": "team",
                "rarity": "uncommon",
                "modifiers": [ { "stat": "BeltSpeed", "op": "mul", "value": 1.5 } ]
              },
              {
                "id": "insured",
                "name": "Insured",
                "description": "Rebuild.",
                "category": "defense",
                "scope": "team",
                "rarity": "rare",
                "rules": ["insured_rebuild"]
              }
            ]
            """,
            "unsorted");
        var draft = new DraftSession(defs, seed: 11, playerCount: 1);
        var expected = Walk(defs, 11, shift: 1, common: 60, uncommon: 30, rare: 10);
        Assert.Equal(expected, draft.Roll(1));
    }

    private static DraftRules ArcadeWithBelt { get; } = new(new[] { "belt_mk1" });

    private static PerkDef[] LoadRepoPerks()
        => PerkCatalog.LoadDir(Path.Combine(FindContentRoot(), PerkCatalog.RelativeDir));

    private static PerkDef[] ThreeCardCatalog()
        => PerkCatalog.Parse(
            """
            [
              {
                "id": "a_common",
                "name": "A",
                "description": "A",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.1 } ]
              },
              {
                "id": "b_uncommon",
                "name": "B",
                "description": "B",
                "category": "facility",
                "scope": "team",
                "rarity": "uncommon",
                "modifiers": [ { "stat": "BeltSpeed", "op": "mul", "value": 1.2 } ]
              },
              {
                "id": "c_rare",
                "name": "C",
                "description": "C",
                "category": "defense",
                "scope": "team",
                "rarity": "rare",
                "rules": ["insured_rebuild"]
              }
            ]
            """,
            "three");

    private static DraftOffer Walk(IReadOnlyList<PerkDef> catalog, uint seed, byte shift, int common, int uncommon, int rare)
    {
        var pool = catalog.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
        var rng = RngStream.Derive(seed, "perks");
        string first = Take(pool, rng, common, uncommon, rare);
        string second = Take(pool, rng, common, uncommon, rare);
        string third = Take(pool, rng, common, uncommon, rare);
        return new DraftOffer(first, second, third);
    }

    private static string Take(List<PerkDef> pool, RngStream rng, int common, int uncommon, int rare)
    {
        int total = 0;
        for (int i = 0; i < pool.Count; i++)
            total += Weight(pool[i].Rarity, common, uncommon, rare);
        int roll = (int)rng.NextBounded((uint)total);
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Weight(pool[i].Rarity, common, uncommon, rare);
            if (roll >= 0) continue;
            string id = pool[i].Id;
            pool.RemoveAt(i);
            return id;
        }

        throw new InvalidOperationException("test walk missed.");
    }

    private static int Weight(PerkRarity rarity, int common, int uncommon, int rare)
        => rarity switch
        {
            PerkRarity.Common => common,
            PerkRarity.Uncommon => uncommon,
            PerkRarity.Rare => rare,
            _ => throw new ArgumentOutOfRangeException(nameof(rarity))
        };

    private static int DistinctCount(DraftOffer offer)
        => new HashSet<string>(StringComparer.Ordinal) { offer.First, offer.Second, offer.Third }.Count;

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, ArchetypeCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/archetypes.json");
    }
}
