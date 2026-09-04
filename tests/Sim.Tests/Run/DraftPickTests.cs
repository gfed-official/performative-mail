using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class DraftPickTests
{
    [Fact]
    public void TeamPerk_GreysOutAfterOnePick()
    {
        var draft = new DraftSession(ThreeCardCatalog(), seed: 1, playerCount: 2);
        var offer = draft.Roll(1);
        Assert.Contains("team_alpha", Cards(offer));
        Assert.True(draft.TryPick(1, "team_alpha", out DraftPickReject first));
        Assert.Equal(DraftPickReject.None, first);
        Assert.True(draft.IsGreyed("team_alpha"));
        Assert.Contains("team_alpha", draft.TeamPicks);
        Assert.False(draft.TryPick(2, "team_alpha", out DraftPickReject greyed));
        Assert.Equal(DraftPickReject.Greyed, greyed);
        string other = offer.First == "team_alpha" ? offer.Second : offer.First;
        Assert.True(draft.TryPick(2, other, out DraftPickReject rest));
        Assert.Equal(DraftPickReject.None, rest);
    }

    [Fact]
    public void BeltPerk_AbsentUntilBeltExists()
    {
        var defs = PerkCatalog.Parse(
            """
            [
              {
                "id": "express_lane",
                "name": "Express Lane",
                "description": "Belts.",
                "category": "facility",
                "scope": "team",
                "rarity": "common",
                "modifiers": [ { "stat": "BeltSpeed", "op": "mul", "value": 1.5 } ],
                "prerequisites": { "builtAny": ["belt_mk1", "belt_mk2"] }
              },
              {
                "id": "personal_a",
                "name": "A",
                "description": "A",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.1 } ]
              },
              {
                "id": "personal_b",
                "name": "B",
                "description": "B",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "InventoryRows", "op": "add", "value": 1 } ]
              },
              {
                "id": "personal_c",
                "name": "C",
                "description": "C",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "MailboxInsertTime", "op": "mul", "value": 0.5 } ]
              }
            ]
            """,
            "belt-gate");

        for (uint seed = 1; seed <= 40; seed++)
        {
            var closed = new DraftSession(defs, seed, playerCount: 1);
            Assert.DoesNotContain("express_lane", Cards(closed.Roll(1)));
        }

        bool seen = false;
        for (uint seed = 1; seed <= 40; seed++)
        {
            var open = new DraftSession(defs, seed, playerCount: 1);
            if (Cards(open.Roll(1, new DraftRules(new[] { "belt_mk1" }))).Contains("express_lane"))
                seen = true;
        }

        Assert.True(seen);
    }

    [Fact]
    public void RepoExpressLane_NeedsBelt()
    {
        var perks = PerkCatalog.LoadDir(Path.Combine(FindContentRoot(), PerkCatalog.RelativeDir));
        var express = Assert.Single(perks, p => p.Id == "express_lane");
        Assert.NotNull(express.Prerequisites);
        Assert.Contains("belt_mk1", express.Prerequisites!.BuiltAny);
        Assert.Contains("belt_mk2", express.Prerequisites.BuiltAny);

        var draft = new DraftSession(perks, seed: 1, playerCount: 1);
        var offer = draft.Roll(1);
        Assert.DoesNotContain("express_lane", Cards(offer));
    }

    [Fact]
    public void ExcludedPerks_NeverCoOccurInOfferOrRun()
    {
        var defs = PerkCatalog.Parse(
            """
            [
              {
                "id": "left",
                "name": "Left",
                "description": "Left",
                "category": "postal",
                "scope": "team",
                "rarity": "common",
                "modifiers": [ { "stat": "QuotaMult", "op": "mul", "value": 0.9 } ],
                "excludes": ["right"]
              },
              {
                "id": "right",
                "name": "Right",
                "description": "Right",
                "category": "postal",
                "scope": "team",
                "rarity": "common",
                "modifiers": [ { "stat": "LetterValue", "op": "mul", "value": 1.1 } ],
                "excludes": ["left"]
              },
              {
                "id": "mid_a",
                "name": "Mid A",
                "description": "A",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.1 } ]
              },
              {
                "id": "mid_b",
                "name": "Mid B",
                "description": "B",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "InventoryRows", "op": "add", "value": 1 } ]
              },
              {
                "id": "mid_c",
                "name": "Mid C",
                "description": "C",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "MailboxInsertTime", "op": "mul", "value": 0.5 } ]
              }
            ]
            """,
            "excludes");

        for (uint seed = 1; seed <= 40; seed++)
        {
            var draft = new DraftSession(defs, seed, playerCount: 1);
            var offer = draft.Roll(1);
            var cards = Cards(offer);
            Assert.False(cards.Contains("left") && cards.Contains("right"));
        }

        string? picked = null;
        DraftSession? run = null;
        for (uint seed = 1; seed <= 40 && picked is null; seed++)
        {
            run = new DraftSession(defs, seed, playerCount: 1);
            var first = Cards(run.Roll(1));
            if (first.Contains("left"))
                picked = "left";
            else if (first.Contains("right"))
                picked = "right";
        }

        Assert.NotNull(picked);
        Assert.NotNull(run);
        Assert.True(run!.TryPick(1, picked!, out _));
        var later = Cards(run.Roll(2));
        if (picked == "left")
            Assert.DoesNotContain("right", later);
        else
            Assert.DoesNotContain("left", later);
    }

    [Fact]
    public void PersonalPerk_TwoPlayersMayShareIt()
    {
        var draft = new DraftSession(ThreeCardCatalog(), seed: 1, playerCount: 2);
        draft.Roll(1);
        Assert.True(draft.TryPick(1, "personal_a", out _));
        Assert.False(draft.IsGreyed("personal_a"));
        Assert.True(draft.TryPick(2, "personal_a", out DraftPickReject reject));
        Assert.Equal(DraftPickReject.None, reject);
        Assert.Equal(new[] { "personal_a" }, draft.PersonalPicks(1));
        Assert.Equal(new[] { "personal_a" }, draft.PersonalPicks(2));
    }

    [Fact]
    public void PersonalPerk_SixthPickRejectedWhenCapReached()
    {
        var defs = StackablePersonalCatalog();
        var draft = new DraftSession(defs, seed: 1, playerCount: 1);
        for (byte shift = 1; shift <= 5; shift++)
        {
            draft.Roll(shift);
            Assert.True(draft.TryPick(1, "stack_me", out DraftPickReject reject));
            Assert.Equal(DraftPickReject.None, reject);
        }

        Assert.Equal(5, draft.PersonalPicks(1).Count);
        draft.Roll(1);
        Assert.False(draft.TryPick(1, "stack_me", out DraftPickReject cap));
        Assert.Equal(DraftPickReject.PersonalCap, cap);
    }

    [Fact]
    public void TeamPerk_StaysUniqueOnLaterRoll()
    {
        DraftSession? draft = null;
        for (uint seed = 1; seed <= 40 && draft is null; seed++)
        {
            var session = new DraftSession(FourCardCatalog(), seed, playerCount: 1);
            if (Cards(session.Roll(1)).Contains("team_alpha"))
                draft = session;
        }

        Assert.NotNull(draft);
        Assert.True(draft!.TryPick(1, "team_alpha", out _));
        Assert.DoesNotContain("team_alpha", Cards(draft.Roll(2)));
    }

    private static PerkDef[] ThreeCardCatalog()
        => PerkCatalog.Parse(
            """
            [
              {
                "id": "team_alpha",
                "name": "Team",
                "description": "Team",
                "category": "facility",
                "scope": "team",
                "rarity": "common",
                "modifiers": [ { "stat": "BeltSpeed", "op": "mul", "value": 1.2 } ]
              },
              {
                "id": "personal_a",
                "name": "A",
                "description": "A",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.1 } ]
              },
              {
                "id": "personal_b",
                "name": "B",
                "description": "B",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "InventoryRows", "op": "add", "value": 1 } ]
              }
            ]
            """,
            "three");

    private static PerkDef[] FourCardCatalog()
        => PerkCatalog.Parse(
            """
            [
              {
                "id": "team_alpha",
                "name": "Team",
                "description": "Team",
                "category": "facility",
                "scope": "team",
                "rarity": "common",
                "modifiers": [ { "stat": "BeltSpeed", "op": "mul", "value": 1.2 } ]
              },
              {
                "id": "personal_a",
                "name": "A",
                "description": "A",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.1 } ]
              },
              {
                "id": "personal_b",
                "name": "B",
                "description": "B",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "InventoryRows", "op": "add", "value": 1 } ]
              },
              {
                "id": "personal_c",
                "name": "C",
                "description": "C",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "MailboxInsertTime", "op": "mul", "value": 0.5 } ]
              }
            ]
            """,
            "four");

    private static PerkDef[] StackablePersonalCatalog()
        => PerkCatalog.Parse(
            """
            [
              {
                "id": "stack_me",
                "name": "Stack",
                "description": "Stack",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "maxStacks": 5,
                "modifiers": [ { "stat": "PlayerSpeed", "op": "mul", "value": 1.1 } ]
              },
              {
                "id": "filler_a",
                "name": "A",
                "description": "A",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "InventoryRows", "op": "add", "value": 1 } ]
              },
              {
                "id": "filler_b",
                "name": "B",
                "description": "B",
                "category": "carrier",
                "scope": "personal",
                "rarity": "common",
                "modifiers": [ { "stat": "MailboxInsertTime", "op": "mul", "value": 0.5 } ]
              }
            ]
            """,
            "stack");

    private static HashSet<string> Cards(DraftOffer offer)
        => new(StringComparer.Ordinal) { offer.First, offer.Second, offer.Third };

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
