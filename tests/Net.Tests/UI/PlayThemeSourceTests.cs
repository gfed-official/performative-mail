namespace PerformativeMail.Net.Tests.UI;

public sealed class PlayThemeSourceTests
{
    [Fact]
    public void PlayTheme_PinsLockedHudAndPauseTokens()
    {
        string theme = ReadGame("PlayTheme.cs");
        Assert.Contains("#1A2433", theme);
        Assert.Contains("0.92f", theme);
        Assert.Contains("CornerRadius = 8", theme);
        Assert.Contains("#3D5A80", theme);
        Assert.Contains("BorderWidth = 2", theme);
        Assert.Contains("#3D7EFF", theme);
        Assert.Contains("#5A93FF", theme);
        Assert.Contains("#E85D3A", theme);
        Assert.Contains("#ECF0F1", theme);
        Assert.Contains("#9AA4B2", theme);
        Assert.Contains("Colors.White", theme);
        Assert.Contains("_shared ??=", theme);
        Assert.Contains("ApplyDanger", theme);
        Assert.Contains("ApplyMuted", theme);
        Assert.DoesNotContain("DebugMenu", theme);
    }

    [Fact]
    public void PauseMenu_UsesPlayTheme_NotDebugChrome()
    {
        string pause = ReadGame("PauseMenu.cs");
        Assert.Contains("PlayTheme.Apply(this)", pause);
        Assert.Contains("PlayTheme.ApplyMuted", pause);
        Assert.Contains("PauseFrame.ConfirmLeaveId", pause);
        Assert.Contains("PlayTheme.ApplyDanger(button)", pause);
        Assert.DoesNotContain("0.12f, 0.13f, 0.16f, 0.96f", pause);
        Assert.DoesNotContain("new StyleBoxFlat", pause);
    }

    [Fact]
    public void Hud_AppliesPlayThemeFontColour()
    {
        string hud = ReadGame("Hud.cs");
        Assert.Contains("PlayTheme.Apply(this)", hud);
        Assert.DoesNotContain("Colors.Gray", hud);
        Assert.DoesNotContain("0.12f, 0.13f, 0.16f", hud);
    }

    [Fact]
    public void DebugMenu_KeepsUtilitarianStyle()
    {
        string debug = ReadGame("DebugMenu.cs");
        Assert.Contains("new Color(0.12f, 0.13f, 0.16f, 0.96f)", debug);
        Assert.Contains("new StyleBoxFlat", debug);
        Assert.DoesNotContain("PlayTheme", debug);
        Assert.DoesNotContain("#1A2433", debug);
        Assert.DoesNotContain("#3D7EFF", debug);
        Assert.DoesNotContain("#E85D3A", debug);
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
