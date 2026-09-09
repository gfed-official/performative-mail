namespace PerformativeMail.Net.Tests.UI;

public sealed class HotbarChromeSourceTests
{
    [Fact]
    public void PinsDistinctLetterAndPackagePlaceholders()
    {
        string chrome = ReadGame("HotbarChrome.cs");
        Assert.Contains("SlotIdle => PlayTheme.Border", chrome);
        Assert.Contains("SlotSelected => PlayTheme.Primary", chrome);
        Assert.Contains("LetterFill = new(0.97f, 0.95f, 0.87f)", chrome);
        Assert.Contains("PackageFill = new(0.75f, 0.48f, 0.24f)", chrome);
        Assert.Contains("28, 16", chrome);
        Assert.Contains("20, 22", chrome);
        Assert.Contains("OverlayIcon.Letter", chrome);
        Assert.Contains("OverlayIcon.Package", chrome);
        Assert.Contains("OverlayIcon.Item", chrome);
        Assert.Contains("OverlayIcon.Hands", chrome);
    }

    [Fact]
    public void HudStrip_IsPersistentAndNumbered()
    {
        string hud = ReadGame("Hud.cs");
        Assert.Contains("HotbarPath = \"HotbarStrip\"", hud);
        Assert.Contains("InputSampler.HotbarSlots", hud);
        Assert.Contains("HotbarSlot", hud);
        Assert.Contains("HotbarSelected=", hud);
        Assert.Contains("icon=", hud);
        Assert.Contains("(index + 1).ToString()", hud);
        string scene = ReadGame(Path.Combine("scenes", "hud.tscn"));
        Assert.Contains("name=\"HotbarDock\"", scene);
        Assert.Contains("name=\"HotbarStrip\"", scene);
        Assert.Contains("anchors_preset = 12", scene);
        Assert.DoesNotContain("name=\"HotbarStrip\" parent=\"Margin/Column/CompassRow\"", scene);
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
