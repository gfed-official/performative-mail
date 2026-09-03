using System.Text.Json.Nodes;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class ContentStarterTests
{
    [Fact]
    public void StarterSet_RepoFiles_HaveExpectedIds()
    {
        string root = FindContentRoot();
        var items = Ids(ItemCatalog.LoadDir(Path.Combine(root, ItemCatalog.RelativeDir)));
        var kinds = Ids(MailKindCatalog.LoadFile(Path.Combine(root, MailKindCatalog.RelativePath)));
        var dests = Ids(DestinationTypeCatalog.LoadFile(Path.Combine(root, DestinationTypeCatalog.RelativePath)));
        var buildings = Ids(BuildingCatalog.LoadDir(Path.Combine(root, BuildingCatalog.RelativeDir)));
        var recipes = Ids(RecipeCatalog.LoadDir(Path.Combine(root, RecipeCatalog.RelativeDir)));
        var shop = Ids(ShopCatalog.LoadDir(Path.Combine(root, ShopCatalog.RelativeDir)));
        var perks = Ids(PerkCatalog.LoadDir(Path.Combine(root, PerkCatalog.RelativeDir)));
        var stamps = Ids(StampCatalog.LoadDir(Path.Combine(root, StampCatalog.RelativeDir)));
        var unlocks = UnlockCatalog.LoadFile(Path.Combine(root, UnlockCatalog.RelativePath));

        Assert.Contains("axe", items);
        Assert.Contains("log", items);
        Assert.Contains("letter", kinds);
        Assert.Contains("small", kinds);
        Assert.Contains("cargo", kinds);
        Assert.Contains("house", dests);
        Assert.Contains("business_dock", dests);
        Assert.Contains("wall_wood", buildings);
        Assert.Contains("chest", buildings);
        Assert.Contains("recipe_wall_wood", recipes);
        Assert.Contains("recipe_chest", recipes);
        Assert.Contains("bandage_x3", shop);
        Assert.Equal(12, perks.Count);
        Assert.Contains("long_legs", perks);
        Assert.Contains("insured", perks);
        Assert.Contains("express_lane", perks);
        Assert.Equal(8, stamps.Count);
        Assert.Contains("double_raids", stamps);
        Assert.Contains("cursed_mail", stamps);
        Assert.Contains(unlocks.Ranks, r => r.Rank == 2 && r.Grants[0].Kind == UnlockKind.Kit);
    }

    [Fact]
    public void MailMix_RepoFile_Shift1LetterShareIs060()
    {
        var mix = MailMixCatalog.LoadFile(Path.Combine(FindContentRoot(), MailMixCatalog.RelativePath));
        var shift1 = Assert.Single(mix.Shifts, s => s.Shift == 1);
        Assert.Equal(0.60, shift1.Shares["letter"]);
    }

    [Fact]
    public void ItemCatalog_DuplicateId_Throws()
    {
        var first = MinimalItem("log");
        var second = MinimalItem("log");
        string json = new JsonArray(first, second).ToJsonString();
        var ex = Assert.Throws<InvalidOperationException>(() => ItemCatalog.Parse(json, "dup-items"));
        Assert.Contains("log", ex.Message);
    }

    [Fact]
    public void RecipeCatalog_ProducesItem_Throws()
    {
        var json = new JsonObject
        {
            ["id"] = "recipe_bad",
            ["produces"] = new JsonObject { ["item"] = "log" },
            ["inputs"] = new JsonArray(new JsonObject { ["item"] = "log", ["count"] = 1 }),
            ["unlockShift"] = 1
        }.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => RecipeCatalog.Parse(json, "produces-item"));
    }

    [Fact]
    public void MailMixCatalog_SharesSumHalf_Throws()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FindContentRoot(), MailMixCatalog.RelativePath)))!;
        node["shifts"]![0]!["shares"] = new JsonObject { ["letter"] = 0.5 };
        var ex = Assert.Throws<InvalidOperationException>(
            () => MailMixCatalog.Parse(node.ToJsonString(), "sum-half"));
        Assert.Contains("0.5", ex.Message);
    }

    private static JsonObject MinimalItem(string id)
    {
        return new JsonObject
        {
            ["id"] = id,
            ["name"] = "Log",
            ["category"] = "material",
            ["grid"] = new JsonArray(1, 1),
            ["maxStack"] = 1,
            ["weightClass"] = "light",
            ["sellPrice"] = 0
        };
    }

    private static HashSet<string> Ids<T>(T[] defs)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var def in defs)
        {
            string? id = def switch
            {
                ItemDef item => item.Id,
                MailKindDef kind => kind.Id,
                DestinationTypeDef dest => dest.Id,
                BuildingDef building => building.Id,
                RecipeDef recipe => recipe.Id,
                ShopItemDef row => row.Id,
                PerkDef perk => perk.Id,
                StampDef stamp => stamp.Id,
                _ => null
            };
            if (id is not null) ids.Add(id);
        }

        return ids;
    }

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
