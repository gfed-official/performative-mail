using System.Text.Json;
using System.Text.RegularExpressions;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class SmokeReportTests
{
    [Fact]
    public void Render_Playing_PinsCompactContract()
    {
        var session = GoldenPlaying(WorldGen.GenerateSmallIsland(0x7F3A9C21));
        var ui = new SmokeReportUi(true, false);
        string json = SmokeReport.Render(session, in ui);

        Assert.Contains("\"state\":\"Playing\"", json);
        Assert.Contains("\"worldHash\":\"0x821670054873680E\"", json);
        Assert.Contains("\"hudPhase\":\"PREP\"", json);
        Assert.Contains("\"hudShift\":\"Shift 1 / 5\"", json);
        Assert.Matches(new Regex("\"id\":\\d"), json);
        Assert.DoesNotContain("pawnCount", json);

        using var doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("pawns").GetArrayLength());
        Assert.False(root.TryGetProperty("pawnCount", out _));
        JsonElement counts = root.GetProperty("worldEntityCounts");
        Assert.Equal(1, counts.GetProperty("postOffices").GetInt32());
        Assert.Equal(1, counts.GetProperty("intakes").GetInt32());
        Assert.Equal(50, counts.GetProperty("houses").GetInt32());
        Assert.Equal(50, counts.GetProperty("mailboxes").GetInt32());
        Assert.True(root.GetProperty("overlayOpen").GetBoolean());
        Assert.False(root.GetProperty("debugOpen").GetBoolean());
    }

    [Fact]
    public void Render_Menu_IsStateOnly()
    {
        string json = SmokeReport.Render(PlaySession.Menu.Instance, default);
        Assert.Equal("{\"state\":\"Menu\"}", json);
    }

    [Fact]
    public void Render_Connecting_IsStateOnly()
    {
        var session = new PlaySession.Connecting(
            new SessionRole.Listening(new HostAdvertisement("127.0.0.1", 7777)),
            null);
        string json = SmokeReport.Render(session, default);
        Assert.Equal("{\"state\":\"Connecting\"}", json);
    }

    [Fact]
    public void Render_Failed_EscapesQuotes()
    {
        const string detail = "host said \"no\"";
        string json = SmokeReport.Render(
            new PlaySession.Failed(new FailReason.BootFailed(detail)),
            default);

        Assert.DoesNotContain("host said 'no'", json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("Failed", doc.RootElement.GetProperty("state").GetString());
        Assert.Equal(detail, doc.RootElement.GetProperty("error").GetString());
        Assert.Equal(2, CountProperties(doc.RootElement));
    }

    [Fact]
    public void Render_Playing_NullWorld_ZeroHashAndCounts()
    {
        string json = SmokeReport.Render(GoldenPlaying(null), default);
        Assert.Contains("\"worldHash\":\"0x0000000000000000\"", json);

        using var doc = JsonDocument.Parse(json);
        JsonElement counts = doc.RootElement.GetProperty("worldEntityCounts");
        Assert.Equal(0, counts.GetProperty("postOffices").GetInt32());
        Assert.Equal(0, counts.GetProperty("intakes").GetInt32());
        Assert.Equal(0, counts.GetProperty("houses").GetInt32());
        Assert.Equal(0, counts.GetProperty("mailboxes").GetInt32());
    }

    [Fact]
    public void Write_WritesRenderOutput()
    {
        string path = Path.Combine(Path.GetTempPath(), "pm-smoke-report-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var session = PlaySession.Menu.Instance;
            var ui = new SmokeReportUi(false, true);
            SmokeReport.Write(path, session, in ui);
            Assert.Equal(SmokeReport.Render(session, in ui), File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static PlaySession.Playing GoldenPlaying(WorldTables? world)
    {
        var id = new EntityId(1);
        var pawn = new PawnView(id, new PlayerPose(1200, 3400, 0, 0), PawnRole.Local, 0, "You");
        var hud = new HudSnapshot(
            RunPhase.Prep,
            1,
            0,
            0,
            new Cents(1820),
            InteractPrompt.None.Instance,
            new Cents(640),
            new Cents(640),
            0);
        return new PlaySession.Playing(
            new SessionRole.Listening(new HostAdvertisement("127.0.0.1", 7777)),
            id,
            new[] { pawn },
            hud,
            world,
            null);
    }

    private static int CountProperties(JsonElement element)
    {
        int count = 0;
        foreach (var _ in element.EnumerateObject())
            count++;
        return count;
    }
}
