namespace PerformativeMail.Net.Tests.UI;

public sealed class MainPlayBootTests
{
    [Fact]
    public void Ready_DoesNotBindHudOrLobby()
    {
        var ready = MethodBody(ReadMain(), "_Ready");
        Assert.DoesNotContain("BindHud", ready);
        Assert.DoesNotContain("BindLobby", ready);
        Assert.DoesNotContain("BindPayday", ready);
        Assert.DoesNotContain("BindDraft", ready);
        Assert.DoesNotContain("BindResults", ready);
        Assert.DoesNotContain("HudBoot.Placeholder", ready);
        Assert.DoesNotContain("LobbyBoot", ready);
        Assert.DoesNotContain("PhaseOverlayBoot", ready);
        Assert.DoesNotContain("BindDebug", ready);
        Assert.DoesNotContain("DebugBoot", ready);
        Assert.Contains("BindOverlay", ready);
        Assert.Contains("performative-mail boot ok", ready);
    }

    [Fact]
    public void InspectHud_StillUsesPlaceholder()
    {
        var inspect = MethodBody(ReadMain(), "InspectHud");
        Assert.Contains("HudBoot.Placeholder", inspect);
    }

    [Fact]
    public void InspectLobby_UsesLobbyBootNotHudPlaceholder()
    {
        var inspect = MethodBody(ReadMain(), "InspectLobby");
        Assert.Contains("LobbyBoot.Arcade", inspect);
        Assert.Contains("LobbyBoot.ArcadeReady", inspect);
        Assert.DoesNotContain("HudBoot.Placeholder", inspect);
        Assert.DoesNotContain("BindHud", inspect);
    }

    [Fact]
    public void BuildLobby_HidesTheScene()
    {
        var build = MethodBody(ReadMain(), "BuildLobby");
        Assert.Contains("_lobby.Visible = false", build);
    }

    [Fact]
    public void InspectOverlays_UsesPhaseBootNotHudPlaceholder()
    {
        var inspect = MethodBody(ReadMain(), "InspectOverlays");
        Assert.Contains("PhaseOverlayBoot.Payday", inspect);
        Assert.Contains("PhaseOverlayBoot.Draft", inspect);
        Assert.Contains("PhaseOverlayBoot.Results", inspect);
        Assert.DoesNotContain("HudBoot.Placeholder", inspect);
        Assert.DoesNotContain("BindHud", inspect);
    }

    [Fact]
    public void BuildPhaseOverlays_HidesPaydayDraftAndResults()
    {
        var build = MethodBody(ReadMain(), "BuildPhaseOverlays");
        Assert.Contains("_payday.Visible = false", build);
        Assert.Contains("_draft.Visible = false", build);
        Assert.Contains("_results.Visible = false", build);
    }

    [Fact]
    public void InspectDebug_UsesDebugBootNotHudPlaceholder()
    {
        var inspect = MethodBody(ReadMain(), "InspectDebug");
        Assert.Contains("DebugBoot.Placeholder", inspect);
        Assert.DoesNotContain("HudBoot.Placeholder", inspect);
        Assert.DoesNotContain("BindHud", inspect);
    }

    [Fact]
    public void Ready_BuildsDebugMenuAfterArgs()
    {
        var ready = MethodBody(ReadMain(), "_Ready");
        Assert.Contains("OS.IsDebugBuild()", ready);
        Assert.Contains("BuildDebugMenu", ready);
        Assert.Contains("PollDebugToggle", MethodBody(ReadMain(), "_PhysicsProcess"));
        Assert.Contains("Key.F3", MethodBody(ReadMain(), "PollDebugToggle"));
    }

    private static string ReadMain()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "game", "Main.cs");
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("game/Main.cs");
    }

    private static string MethodBody(string source, string name)
    {
        var needle = "void " + name + "(";
        int start = source.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0, "missing method " + name);
        int brace = source.IndexOf('{', start);
        Assert.True(brace >= 0, "missing body for " + name);
        int depth = 0;
        for (int i = brace; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(brace, i - brace + 1);
            }
        }

        throw new InvalidOperationException("unbalanced body for " + name);
    }
}
