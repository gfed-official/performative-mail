using PerformativeMail.App;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class BuildModeSessionTests
{
    private static readonly TileCoord PlaceTile = new(7, 2);

    [Fact]
    public void CreateDebug_WiresConstructRegistry()
    {
        var boot = ArcadeSession.CreateDebug();
        Assert.NotNull(boot.World.Constructs);
        Assert.Equal(7, boot.World.Constructs!.Count);
    }

    [Fact]
    public void HostDebug_BuildModePlacesWallIntoSim()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, 8);

        Assert.IsType<PlaySession.Playing>(host.State);
        Assert.True(host.TryTeleportToIntake());
        for (int i = 0; i < 3; i++)
            Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "log")));
        Assert.True(host.TryOpenBuild());
        Assert.True(host.Build!.Select("wall_wood"));
        Assert.True(host.TryGhost(PlaceTile, out var hint));
        Assert.True(hint.Valid);
        Assert.Equal("", hint.Reason);
        Assert.True(host.TryPlaceAt(PlaceTile));
        Assert.Contains(host.PlacedConstructs(), row => row.DefId == "wall_wood" && row.Tile == PlaceTile);
        Assert.Equal(8, HostCount(host));
    }

    [Fact]
    public void HostDebug_StreetGhost_MatchesSimReject()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, 8);

        Assert.True(host.TryTeleportToIntake());
        for (int i = 0; i < 3; i++)
            Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "log")));
        Assert.True(host.TryOpenBuild());
        Assert.True(host.Build!.Select("wall_wood"));
        var street = new TileCoord(5, 6);
        Assert.True(host.TryGhost(street, out var hint));
        Assert.False(hint.Valid);
        Assert.Equal(BuildRejectText.Of(PlaceReject.Street), hint.Reason);
        Assert.False(host.TryPlaceAt(street));
        Assert.Empty(host.PlacedConstructs());
    }

    [Fact]
    public void HostDebug_SlopeGhost_MatchesSimReject()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, 8);

        Assert.True(host.TryTeleportToIntake());
        for (int i = 0; i < 3; i++)
            Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "log")));
        Assert.True(host.TrySetPlacementHeight(new TileCoord(8, 2), 254));
        Assert.True(host.TryOpenBuild());
        Assert.True(host.Build!.Select("wall_wood"));
        Assert.True(host.TryGhost(PlaceTile, out var hint));
        Assert.False(hint.Valid);
        Assert.Equal(BuildRejectText.Of(PlaceReject.Slope), hint.Reason);
        Assert.False(host.TryPlaceAt(PlaceTile));
        Assert.Empty(host.PlacedConstructs());
    }

    [Fact]
    public void HostDebug_MissingInputGhost_MatchesSimReject()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, 8);

        Assert.True(host.TryTeleportToIntake());
        Assert.True(host.TryOpenBuild());
        Assert.True(host.Build!.Select("wall_wood"));
        Assert.True(host.TryGhost(PlaceTile, out var hint));
        Assert.False(hint.Valid);
        Assert.Equal(BuildRejectText.Of(PlaceReject.MissingInput), hint.Reason);
        Assert.False(host.TryPlaceAt(PlaceTile));
        Assert.Empty(host.PlacedConstructs());
    }

    [Fact]
    public void HostDebug_StraightBeltDrag_EmitsOnePlaceLine()
    {
        var from = new TileCoord(6, 2);
        var to = new TileCoord(8, 2);
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, 8);

        Assert.True(host.TryTeleportToIntake());
        for (int i = 0; i < 3; i++)
        {
            Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "plank")));
            Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "iron_ingot")));
        }

        Assert.True(host.TryOpenBuild());
        Assert.True(host.Build!.Select("belt_mk1"));
        host.Build.Rotate();
        int before = HostCount(host);
        Assert.True(host.TryDragPlace(from, to));
        Assert.Equal(1, host.PlaceLineRequests);
        Assert.Equal(before + 3, HostCount(host));
        Assert.Contains(host.PlacedConstructs(), row => row.DefId == "belt_mk1" && row.Tile == from);
        Assert.Contains(host.PlacedConstructs(), row => row.DefId == "belt_mk1" && row.Tile == new TileCoord(7, 2));
        Assert.Contains(host.PlacedConstructs(), row => row.DefId == "belt_mk1" && row.Tile == to);
    }

    private static int HostCount(PlaySessionMachine host)
    {
        Assert.True(host.TryHostWorld(out var world, out _));
        return world.Constructs!.Count;
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
