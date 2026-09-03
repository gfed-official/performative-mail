using System.Text.Json.Nodes;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class ContentFilesTests
{
    [Fact]
    public void Validate_RepoContent_Succeeds()
    {
        ContentFiles.Validate(FindContentRoot());
    }

    [Fact]
    public void ArchetypeCatalog_RepoFile_SmallIslandSumIsInGrownBand()
    {
        var defs = ArchetypeCatalog.LoadFile(Path.Combine(FindContentRoot(), ArchetypeCatalog.RelativePath));
        Assert.Single(defs);
        Assert.Equal("small_island", defs[0].Id);
        Assert.Equal(SettlementSize.Medium, defs[0].Towns[0].Size);
        Assert.Equal(50, defs[0].DistrictHouseTotal);
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
        var ex = Assert.Throws<InvalidOperationException>(() => ContentFiles.Validate(root));
        Assert.Contains("districtHouseCounts sum 3", ex.Message);
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
        string src = FindContentRoot();
        string dest = Path.Combine(Path.GetTempPath(), "pm-u21-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dest, "world"));
        File.Copy(Path.Combine(src, StreetCatalog.RelativePath), Path.Combine(dest, StreetCatalog.RelativePath));
        File.Copy(Path.Combine(src, BalanceCatalog.RelativePath), Path.Combine(dest, BalanceCatalog.RelativePath));
        string json = MutateRepoArchetypes(mutate);
        File.WriteAllText(Path.Combine(dest, ArchetypeCatalog.RelativePath), json);
        return dest;
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
