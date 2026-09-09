namespace PerformativeMail.Net.Tests.UI;

public sealed class CompassSourceTests
{
    [Fact]
    public void HudStrip_SitsBelowStatusAndLeavesHotbarAlone()
    {
        string hud = ReadGame("Hud.cs");
        Assert.Contains("CompassPath = \"CompassStrip\"", hud);
        Assert.Contains("BindCompass", hud);
        Assert.Contains("CompassFacing=", hud);
        Assert.Contains("CompassDistrict=", hud);
        Assert.Contains("CompassSlots=", hud);
        Assert.Contains("DistrictSwatch.Of(district)", hud);
        Assert.Contains("CompassFrame.SlotCount", hud);
        Assert.DoesNotContain("HotbarPath = \"CompassStrip\"", hud);

        string scene = ReadGame(Path.Combine("scenes", "hud.tscn"));
        Assert.Contains("name=\"CompassRow\"", scene);
        Assert.Contains("name=\"CompassStrip\"", scene);
        Assert.Contains("name=\"CompassDistrict\"", scene);
        Assert.Contains("name=\"CompassFacing\"", scene);
        Assert.Contains("parent=\"Margin/Column/CompassRow\"", scene);
        int status = scene.IndexOf("name=\"StatusRow\"", StringComparison.Ordinal);
        int compass = scene.IndexOf("name=\"CompassRow\"", StringComparison.Ordinal);
        int interact = scene.IndexOf("name=\"Interact\"", StringComparison.Ordinal);
        int hotbar = scene.IndexOf("name=\"HotbarDock\"", StringComparison.Ordinal);
        Assert.InRange(status, 0, compass - 1);
        Assert.InRange(compass, status + 1, interact - 1);
        Assert.InRange(interact, compass + 1, hotbar - 1);
    }

    [Fact]
    public void Main_BindsCompassFromWorldAndLocalPose()
    {
        string main = ReadGame("Main.cs");
        Assert.Contains("BindCompass(playing)", main);
        Assert.Contains("CompassFrame.From(playing.World", main);
        Assert.Contains("CompassFrame.SameDisplay", main);
        Assert.Contains("_hud.BindCompass", main);
        Assert.Contains("CompassBoot.Placeholder()", main);
        Assert.DoesNotContain("HudSnapshot(", MethodBody(main, "BindCompass"));
    }

    private static string MethodBody(string source, string name)
    {
        string header = "private void " + name + "(";
        int start = source.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, "missing " + name);
        int open = source.IndexOf('{', start);
        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(open, i - open + 1);
            }
        }

        throw new InvalidOperationException(name);
    }

    private static string ReadGame(string file)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", file);
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/" + file);
    }
}
