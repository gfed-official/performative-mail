using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class DebugWorldSessionTests
{
    [Fact]
    public void Create_StillOffersGoldenSmallIsland()
    {
        var boot = ArcadeSession.Create();
        Assert.Equal(0x821670054873680EUL, boot.Offer.WorldHash);
        Assert.Equal(50, boot.Tables.Houses.Length);
        Assert.Equal(0x821670054873680EUL, WorldHash.Compute(boot.Tables));
    }

    [Fact]
    public void CreateDebug_OffersTwoHouseStub()
    {
        var boot = ArcadeSession.CreateDebug();
        Assert.Equal(2, boot.Tables.Houses.Length);
        Assert.Equal(DebugWorld.Hash, boot.Offer.WorldHash);
        Assert.Equal(WorldHash.Compute(boot.Tables), boot.Offer.WorldHash);
        Assert.NotEqual(0x821670054873680EUL, boot.Offer.WorldHash);
        Assert.Equal(DebugWorld.StreetName, boot.Tables.Streets[0].Name);
    }

    [Fact]
    public void HostDebug_ReachesPlayingOnStubWorld()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, 8);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(play.World);
        Assert.Equal(2, play.World.Houses.Length);
        Assert.Equal(DebugWorld.Hash, WorldHash.Compute(play.World));
        Assert.NotEqual(0x821670054873680EUL, WorldHash.Compute(play.World));
        Assert.Equal(SpawnRing.CentreOf(WorldAtlas.FromTables(play.World)), play.Pawns[0].Pose);
    }

    [Fact]
    public void Render_DebugWorld_KeepsSchemaWithTwoHouses()
    {
        var session = GoldenPlaying(DebugWorld.Tables());
        string json = SmokeReport.Render(session, default);

        Assert.Contains("\"state\":\"Playing\"", json);
        Assert.Contains("\"worldHash\":\"0x4CF184F2FA4D4EEE\"", json);
        Assert.DoesNotContain("pawnCount", json);
        Assert.DoesNotContain("0x821670054873680E", json);

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("worldEntityCounts").GetProperty("postOffices").GetInt32());
        Assert.Equal(1, root.GetProperty("worldEntityCounts").GetProperty("intakes").GetInt32());
        Assert.Equal(2, root.GetProperty("worldEntityCounts").GetProperty("houses").GetInt32());
        Assert.Equal(2, root.GetProperty("worldEntityCounts").GetProperty("mailboxes").GetInt32());
        Assert.False(root.TryGetProperty("pawnCount", out _));
    }

    [Fact]
    public void Host_BuildsWorldAfterLeaveInsideTry()
    {
        string source = ReadPlaySessionMachine();
        Assert.Contains("StartHost(ArcadeSession.Create)", source);
        Assert.DoesNotContain("StartHost(ArcadeSession.Create())", source);
        Assert.Contains("StartHost(ArcadeSession.CreateDebug)", source);
        string start = SliceMethod(source, "StartHost");
        int leave = start.IndexOf("Leave();", StringComparison.Ordinal);
        int tryAt = start.IndexOf("try", StringComparison.Ordinal);
        int create = start.IndexOf("create();", StringComparison.Ordinal);
        Assert.True(leave >= 0 && tryAt > leave && create > tryAt);
        Assert.Contains("BootFailed", start);
    }

    private static string ReadPlaySessionMachine()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "src", "App", "PlaySessionMachine.cs");
                if (File.Exists(candidate))
                    return File.ReadAllText(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("src/App/PlaySessionMachine.cs");
    }

    private static string SliceMethod(string source, string name)
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
                    return source.Substring(start, i - start + 1);
            }
        }

        throw new InvalidOperationException("unbalanced body for " + name);
    }

    private static PlaySession.Playing GoldenPlaying(WorldTables world)
    {
        var id = new EntityId(1);
        var pawn = new PawnView(id, new PlayerPose(500, 500, 0, 0), PawnRole.Local, 0, "You");
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

    private static void Pump(PlaySessionMachine host, ref TimeSpan now, int steps)
    {
        var tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);
        for (int i = 0; i < steps; i++)
        {
            now += tick;
            host.Pump(now, MoveIntent.Idle);
        }
    }
}
