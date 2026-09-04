using System.Text.Json.Nodes;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class ContentFilesTests
{
    [Fact]
    public void Validate_RepoContent_Succeeds()
    {
        ContentFiles.Load(FindContentRoot());
    }

    [Fact]
    public void ArchetypeCatalog_RepoFile_SmallIslandSumIsInGrownBand()
    {
        var defs = ArchetypeCatalog.LoadFile(Path.Combine(FindContentRoot(), ArchetypeCatalog.RelativePath));
        Assert.Single(defs);
        Assert.Equal("small_island", defs[0].Id);
        Assert.Equal(SettlementSize.Medium, defs[0].Towns[0].Size);
        Assert.Equal(50, defs[0].DistrictHouseTotal);
        Assert.Equal(new PopulationBand(13, 50), SettlementBands.MediumAfterDistricts);
        Assert.True(SettlementBands.Grown(SettlementSize.Medium).Contains(50));
    }

    [Fact]
    public void ArchetypeCatalog_BadDistrictSum_Throws()
    {
        string json = MutateRepoArchetypes(def => def["districtHouseCounts"] = new JsonArray(1, 1, 1));
        var ex = Assert.Throws<InvalidOperationException>(() => ArchetypeCatalog.Parse(json, "bad-sum"));
        Assert.Contains("districtHouseCounts sum 3", ex.Message);
        Assert.Contains("medium town band 13-50", ex.Message);
    }

    [Fact]
    public void ContentFiles_PlantedBadDistrictSum_FailsValidate()
    {
        string root = PlantMutatedArchetypes(def => def["districtHouseCounts"] = new JsonArray(1, 1, 1));
        var ex = Assert.Throws<InvalidOperationException>(() => ContentFiles.Load(root));
        Assert.Contains("districtHouseCounts sum 3", ex.Message);
    }

    [Fact]
    public void ContentFiles_PlantedDanglingRecipeBuilding_FailsValidate()
    {
        string root = PlantContentTree();
        string path = Path.Combine(root, "recipes", "recipe_chest.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["produces"]!["building"] = "no_such_building";
        File.WriteAllText(path, node.ToJsonString());
        var ex = Assert.Throws<InvalidOperationException>(() => ContentFiles.Load(root));
        Assert.Contains("no_such_building", ex.Message);
    }

    [Fact]
    public void ContentFiles_PlantedUnknownStat_FailsValidate()
    {
        string root = PlantContentTree();
        string path = Path.Combine(root, "perks", "long_legs.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["modifiers"]![0]!["stat"] = "NotAStat";
        File.WriteAllText(path, node.ToJsonString());
        var ex = Assert.Throws<InvalidOperationException>(() => ContentFiles.Load(root));
        Assert.Contains("NotAStat", ex.Message);
    }

    [Fact]
    public void ContentFiles_PlantedUnknownRuleFlag_FailsValidate()
    {
        string root = PlantContentTree();
        string path = Path.Combine(root, "stamps", "double_raids.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["rules"] = new JsonArray("not_a_flag");
        File.WriteAllText(path, node.ToJsonString());
        var ex = Assert.Throws<InvalidOperationException>(() => ContentFiles.Load(root));
        Assert.Contains("not_a_flag", ex.Message);
    }

    [Fact]
    public void ArchetypeCatalog_UnknownTownSize_Throws()
    {
        string json = MutateRepoArchetypes(def =>
        {
            def["towns"] = new JsonArray(new JsonObject { ["size"] = "village", ["count"] = 1 });
        });
        var ex = Assert.Throws<InvalidOperationException>(() => ArchetypeCatalog.Parse(json, "unknown-size"));
        Assert.Contains("unknown id 'village'", ex.Message);
    }

    [Fact]
    public void ArchetypeCatalog_UnknownId_Throws()
    {
        string json = MutateRepoArchetypes(def => def["id"] = "SmallIsland");
        var ex = Assert.Throws<InvalidOperationException>(() => ArchetypeCatalog.Parse(json, "unknown-id"));
        Assert.Contains("unknown id 'SmallIsland'", ex.Message);
    }

    [Fact]
    public void BalanceCatalog_RepoFile_HasRequiredKeys()
    {
        var table = BalanceCatalog.LoadFile(Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath));
        Assert.Equal(new[] { 600, 1100, 1800, 2700, 4000 }, table.BaseQuota);
        Assert.Equal(0.65, table.PlayerScaleExponent);
        Assert.Equal(150, table.InterestRadius);
        Assert.Equal(5, table.PrepSeconds.Length);
    }

    [Fact]
    public void BalanceCatalog_MissingKey_Throws()
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath)))!;
        node.AsObject().Remove("baseQuota");
        var ex = Assert.Throws<InvalidOperationException>(() => BalanceCatalog.Parse(node.ToJsonString(), "missing-key"));
        Assert.Contains("baseQuota", ex.Message);
    }

    [Fact]
    public void StreetCatalog_RepoFile_StillLoadsFromContentRoot()
    {
        var names = StreetCatalog.LoadFile(Path.Combine(FindContentRoot(), StreetCatalog.RelativePath));
        Assert.True(names.Length >= 120);
        Assert.Contains("Larch Lane", names);
    }

    private static string PlantMutatedArchetypes(Action<JsonObject> mutate)
    {
        string dest = PlantContentTree();
        string json = MutateRepoArchetypes(mutate);
        File.WriteAllText(Path.Combine(dest, ArchetypeCatalog.RelativePath), json);
        return dest;
    }

    private static string PlantContentTree()
    {
        string dest = Path.Combine(Path.GetTempPath(), "pm-u22-" + Guid.NewGuid().ToString("N"));
        CopyTree(FindContentRoot(), dest);
        return dest;
    }

    private static void CopyTree(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (string dir in Directory.GetDirectories(src))
            CopyTree(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static string MutateRepoArchetypes(Action<JsonObject> mutate)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(FindContentRoot(), ArchetypeCatalog.RelativePath)))!;
        var def = node.AsArray()[0]!.AsObject();
        mutate(def);
        return node.ToJsonString();
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
