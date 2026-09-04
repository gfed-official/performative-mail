namespace PerformativeMail.Net.Tests.UI;

public sealed class MainPlayBootTests
{
    [Fact]
    public void Render_Playing_BindsHudFromPlaying()
    {
        var render = MethodBody(ReadMain(), "Render");
        Assert.Contains("BindHud(playing.Hud)", render);
        Assert.Contains("_world.Sync(playing.World)", render);
        Assert.DoesNotContain("HudBoot.Placeholder", render);
    }

    [Fact]
    public void BuildHud_StartsHidden()
    {
        Assert.Contains("_hud.Visible = false", MethodBody(ReadMain(), "BuildHud"));
    }

    [Fact]
    public void BuildWorld_DropsFortyMetrePlane()
    {
        var build = MethodBody(ReadMain(), "BuildWorld");
        Assert.DoesNotContain("PlaneMesh", build);
        Assert.Contains("WorldStage", build);
    }

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

    [Fact]
    public void PhysicsProcess_PollsPauseMenu()
    {
        var body = MethodBody(ReadMain(), "_PhysicsProcess");
        Assert.Contains("PollPause", body);
        Assert.Contains("InputSampler.Sample", body);
    }

    [Fact]
    public void Playing_HidesMenuChromeAndUsesPerPawnCameras()
    {
        var render = MethodBody(ReadMain(), "Render");
        Assert.Contains("ShowMenuChrome(false)", render);
        Assert.Contains("_pawns.Sync(playing.Pawns, _look.PitchRadians)", render);
        Assert.DoesNotContain("ApplyFirstPersonCamera", ReadMain());
        Assert.Contains("BindHud(playing.Hud)", render);
        Assert.Contains("_world.Sync(playing.World)", render);
        Assert.DoesNotContain("0f, 9f, 8f", ReadMain());
        Assert.DoesNotContain("_leave", ReadMain());
        Assert.Contains("ShowMenuChrome(true)", render);
        Assert.Contains("UseMenuCamera", render);
    }

    [Fact]
    public void BuildWorld_UsesMenuCameraFallback()
    {
        var build = MethodBody(ReadMain(), "BuildWorld");
        Assert.Contains("MenuCamera", build);
        Assert.Contains("FirstPersonLook.EyeHeightMeters", build);
        Assert.DoesNotContain("PlaneMesh", build);
    }

    [Fact]
    public void PauseChoice_StillLeavesSession()
    {
        var body = MethodBody(ReadMain(), "OnPauseChoice");
        Assert.Contains("_session.Leave()", body);
        Assert.Contains("_pause.WantsLeave", body);
    }

    [Fact]
    public void OpenPause_SurfacesHostAdvertisement()
    {
        var body = MethodBody(ReadMain(), "BindPause");
        Assert.Contains("listening.Advertisement", body);
        Assert.Contains("Join ", body);
        Assert.Contains("BindPause(state)", MethodBody(ReadMain(), "OpenPause"));
    }

    [Fact]
    public void WriteReport_DelegatesToSmokeReport()
    {
        var write = SliceMethod(ReadMain(), "WriteReport");
        Assert.Contains("SmokeReport.Write", write);
        Assert.DoesNotContain("json.Append", write);
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

    private static string SliceMethod(string source, string name)
    {
        var needle = "void " + name + "(";
        int start = source.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(start >= 0, "missing method " + name);
        int arrow = source.IndexOf("=>", start);
        int brace = source.IndexOf('{', start);
        if (arrow >= 0 && (brace < 0 || arrow < brace))
        {
            int end = source.IndexOf(';', arrow);
            Assert.True(end >= 0, "missing body for " + name);
            return source.Substring(start, end - start + 1);
        }

        return MethodBody(source, name);
    }
}
