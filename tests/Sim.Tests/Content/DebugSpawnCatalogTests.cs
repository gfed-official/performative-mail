using System.Text.Json.Nodes;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class DebugSpawnCatalogTests
{
    [Fact]
    public void From_RepoContent_ListsEveryItemKindAndBike()
    {
        var (bundle, catalog) = Load();
        DebugSpawnCoverage.RequireComplete(bundle, catalog);

        var items = Ids(catalog, DebugSpawnKind.Item);
        var mail = Ids(catalog, DebugSpawnKind.Mail);
        Assert.Equal(bundle.Items.Select(i => i.Id).OrderBy(id => id, StringComparer.Ordinal), items);
        Assert.Equal(bundle.Kinds.Select(k => k.Id).OrderBy(id => id, StringComparer.Ordinal), mail);
        Assert.Contains(catalog.Rows, r => r.Kind == DebugSpawnKind.Bike && r.Id.ContentId == "bike");
        Assert.Contains(catalog.Rows, r => r.Id.ContentId == "axe");
        Assert.Contains(catalog.Rows, r => r.Id.ContentId == "letter");
    }

    [Fact]
    public void RequireComplete_UnclaimedProperty_Throws()
    {
        var (bundle, catalog) = Load();
        var coverage = catalog.Coverage.Where(c => c.BundleProperty != nameof(ContentBundle.Streets)).ToArray();
        var ex = Assert.Throws<InvalidOperationException>(
            () => DebugSpawnCoverage.RequireComplete(bundle, new DebugSpawnCatalog(catalog.Rows, coverage)));
        Assert.Contains(nameof(ContentBundle.Streets), ex.Message);
    }

    [Fact]
    public void RequireComplete_MissingItemRow_Throws()
    {
        var (bundle, catalog) = Load();
        var rows = catalog.Rows.Where(r => r.Id.ContentId != "axe").ToArray();
        var ex = Assert.Throws<InvalidOperationException>(
            () => DebugSpawnCoverage.RequireComplete(bundle, new DebugSpawnCatalog(rows, catalog.Coverage)));
        Assert.Contains("Items", ex.Message);
    }

    [Fact]
    public void RequireComplete_MissingBike_Throws()
    {
        var (bundle, catalog) = Load();
        var rows = catalog.Rows.Where(r => r.Kind != DebugSpawnKind.Bike).ToArray();
        var ex = Assert.Throws<InvalidOperationException>(
            () => DebugSpawnCoverage.RequireComplete(bundle, new DebugSpawnCatalog(rows, catalog.Coverage)));
        Assert.Contains("Bike", ex.Message);
    }

    [Fact]
    public void From_PlantedItem_AppearsWithoutCatalogEdits()
    {
        string root = PlantContentTree();
        File.WriteAllText(
            Path.Combine(root, "items", "debug_widget.json"),
            new JsonArray(MinimalItem("debug_widget")).ToJsonString());

        var bundle = ContentFiles.Load(root);
        var ids = ContentIdMap.Build(bundle);
        var catalog = DebugSpawnCatalog.From(bundle, ids);
        DebugSpawnCoverage.RequireComplete(bundle, catalog);
        Assert.Contains(catalog.Rows, r => r.Kind == DebugSpawnKind.Item && r.Id.ContentId == "debug_widget");
        Assert.True(ids.TryItem("debug_widget", out _));
    }

    private static (ContentBundle Bundle, DebugSpawnCatalog Catalog) Load()
    {
        var bundle = ContentFiles.Load(FindContentRoot());
        var ids = ContentIdMap.Build(bundle);
        ContentStackCatalog.From(bundle, ids);
        return (bundle, DebugSpawnCatalog.From(bundle, ids));
    }

    private static IEnumerable<string> Ids(DebugSpawnCatalog catalog, DebugSpawnKind kind)
        => catalog.Rows.Where(r => r.Kind == kind).Select(r => r.Id.ContentId);

    private static JsonObject MinimalItem(string id) =>
        new()
        {
            ["id"] = id,
            ["name"] = "Debug Widget",
            ["category"] = "material",
            ["grid"] = new JsonArray(1, 1),
            ["maxStack"] = 1,
            ["weightClass"] = "light",
            ["sellPrice"] = 0
        };

    private static string PlantContentTree()
    {
        string dest = Path.Combine(Path.GetTempPath(), "pm-spawn-" + Guid.NewGuid().ToString("N"));
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
