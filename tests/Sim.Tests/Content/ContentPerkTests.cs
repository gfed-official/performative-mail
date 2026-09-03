using PerformativeMail.Sim.Content;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class ContentPerkTests
{
    [Fact]
    public void PerkCatalog_UnknownStat_Throws()
    {
        const string json = """
            {
              "id": "bad_perk",
              "name": "Bad Perk",
              "description": "Uses a stat that is not in the closed enum.",
              "category": "carrier",
              "scope": "personal",
              "rarity": "common",
              "modifiers": [ { "stat": "NotAStat", "op": "mul", "value": 1.1 } ]
            }
            """;
        var ex = Assert.Throws<InvalidOperationException>(() => PerkCatalog.Parse(json, "unknown-stat"));
        Assert.Contains("NotAStat", ex.Message);
    }

    [Fact]
    public void StampCatalog_UnknownRuleFlag_Throws()
    {
        const string json = """
            {
              "id": "bad_stamp",
              "name": "Bad Stamp",
              "tier": 1,
              "scoreMult": 1.1,
              "modifiers": [ { "stat": "DeliverySeconds", "op": "add", "value": -10 } ],
              "rules": ["not_a_flag"]
            }
            """;
        var ex = Assert.Throws<InvalidOperationException>(() => StampCatalog.Parse(json, "unknown-flag"));
        Assert.Contains("not_a_flag", ex.Message);
    }

    [Fact]
    public void Stats_UnknownId_FailsParse()
    {
        Assert.False(Stats.TryParse("NotAStat", out _));
        Assert.False(Stats.TryParse("playerspeed", out _));
        Assert.False(Stats.TryParse("0", out _));
        Assert.True(Stats.TryParse("BeltSpeed", out var stat));
        Assert.Equal(Stat.BeltSpeed, stat);
    }

    [Fact]
    public void RuleFlags_UnknownId_FailsParse()
    {
        Assert.False(RuleFlags.TryParse("not_a_flag", out _));
        Assert.False(RuleFlags.TryParse("InsuredRebuild", out _));
        Assert.True(RuleFlags.TryParse("insured_rebuild", out var flag));
        Assert.Equal(RuleFlag.InsuredRebuild, flag);
    }

    [Fact]
    public void UnlockCatalog_UnknownKind_Throws()
    {
        const string json = """
            { "ranks": [ { "rank": 2, "unlocks": [ { "vehicle": "bike" } ] } ] }
            """;
        var ex = Assert.Throws<InvalidOperationException>(() => UnlockCatalog.Parse(json, "unknown-unlock"));
        Assert.Contains("vehicle", ex.Message);
    }

    [Fact]
    public void PerkCatalog_DuplicateId_Throws()
    {
        const string json = """
            [
              {
                "id": "long_legs",
                "name": "Long Legs",
                "description": "Dup",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.12 } ]
              },
              {
                "id": "long_legs",
                "name": "Long Legs 2",
                "description": "Dup",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.12 } ]
              }
            ]
            """;
        var ex = Assert.Throws<InvalidOperationException>(() => PerkCatalog.Parse(json, "dup-perk"));
        Assert.Contains("long_legs", ex.Message);
    }
}
