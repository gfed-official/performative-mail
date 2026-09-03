using System.Text.Json.Nodes;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class ContentReloadTests
{
    [Fact]
    public void Open_RepoContent_ReadsWalkSpeedFromDisk()
    {
        var session = ContentSession.Open(FindContentRoot());
        Assert.Equal(5.0, session.Bundle.Balance.WalkSpeed);
        Assert.Contains(session.Bundle.Perks, perk => perk.Id == "long_legs");
    }

    [Fact]
    public void Reload_AfterBalanceEdit_PicksUpNewWalkSpeed()
    {
        string root = PlantContentTree();
        var session = ContentSession.Open(root);
        var before = session.Bundle;
        Assert.Equal(5.0, before.Balance.WalkSpeed);

        WriteWalkSpeed(root, 9.5);
        var result = session.Reload();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(9.5, session.Bundle.Balance.WalkSpeed);
        Assert.Equal(5.0, before.Balance.WalkSpeed);
        Assert.NotSame(before, session.Bundle);
    }

    [Fact]
    public void Reload_PlantedUnknownStat_KeepsPreviousBundle()
    {
        string root = PlantContentTree();
        var session = ContentSession.Open(root);
        var before = session.Bundle;

        string path = Path.Combine(root, "perks", "long_legs.json");
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["modifiers"]![0]!["stat"] = "NotAStat";
        File.WriteAllText(path, node.ToJsonString());

        var result = session.Reload();
        Assert.False(result.Succeeded);
        Assert.Contains("NotAStat", result.Error);
        Assert.Same(before, session.Bundle);
        Assert.Equal(5.0, session.Bundle.Balance.WalkSpeed);
    }

    [Fact]
    public void Watch_BalanceEditOnDisk_ReloadsWithoutRestart()
    {
        string root = PlantContentTree();
        var session = ContentSession.Open(root);
        using var watch = session.Watch();
        using var reloaded = new ManualResetEventSlim(false);
        watch.Reloaded += () => reloaded.Set();

        WriteWalkSpeed(root, 8.25);

        Assert.True(reloaded.Wait(TimeSpan.FromSeconds(8)), "FileSystemWatcher did not fire Reload.");
        Assert.Equal(8.25, session.Bundle.Balance.WalkSpeed);
    }

    private static void WriteWalkSpeed(string root, double walkSpeed)
    {
        string path = Path.Combine(root, BalanceCatalog.RelativePath);
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        node["walkSpeed"] = walkSpeed;
        File.WriteAllText(path, node.ToJsonString());
    }

    private static string PlantContentTree()
    {
        string dest = Path.Combine(Path.GetTempPath(), "pm-u24-" + Guid.NewGuid().ToString("N"));
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
