using PerformativeMail.Client.UI;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.UI;

public sealed class MapPingBoardTests
{
    [Fact]
    public void TryPlace_KeepsPingAndDropsSecondInsideOneSecond()
    {
        var board = new MapPingBoard();
        Assert.True(board.TryPlace(MapBoot.PingTile, MapPingKind.Default, 0, out var first));
        Assert.Equal(1, first.Id);
        Assert.Equal(MapBoot.PingTile, first.Tile);
        Assert.Equal(MapPingKind.Default, first.Kind);
        Assert.Single(board.Visible);

        Assert.False(board.TryPlace(new TileCoord(1, 1), MapPingKind.Danger, (uint)(MapPingBoard.RateLimitTicks - 1), out _));
        Assert.Single(board.Visible);
        Assert.Equal(first, board.Visible[0]);
    }

    [Fact]
    public void TryPlace_AllowsSecondPingAfterOneSecond()
    {
        var board = new MapPingBoard();
        Assert.True(board.TryPlace(MapBoot.PingTile, MapPingKind.DeliverHere, 0, out _));
        Assert.True(board.TryPlace(new TileCoord(2, 3), MapPingKind.BuildHere, (uint)MapPingBoard.RateLimitTicks, out var second));
        Assert.Equal(2, board.Visible.Count);
        Assert.Equal(MapPingKind.BuildHere, second.Kind);
        Assert.Equal(new TileCoord(2, 3), second.Tile);
    }

    [Fact]
    public void Expire_DropsPingsAfterTenSeconds()
    {
        var board = new MapPingBoard();
        Assert.True(board.TryPlace(MapBoot.PingTile, MapPingKind.NeedMaterials, 0, out _));
        board.Expire((uint)(MapPingBoard.LifetimeTicks - 1));
        Assert.Single(board.Visible);
        board.Expire((uint)MapPingBoard.LifetimeTicks);
        Assert.Empty(board.Visible);
    }

    [Fact]
    public void LifetimeAndRateLimit_MatchSpec()
    {
        Assert.Equal(10, MapPingBoard.LifetimeSeconds);
        Assert.Equal(1, MapPingBoard.RateLimitSeconds);
        Assert.Equal(300, MapPingBoard.LifetimeTicks);
        Assert.Equal(30, MapPingBoard.RateLimitTicks);
    }

    [Fact]
    public void Observe_AddsRemotePingWithoutConsumingRateLimit()
    {
        var board = new MapPingBoard();
        board.Observe(new MapPing(7, new TileCoord(1, 2), MapPingKind.Danger, 0));
        Assert.True(board.TryPlace(MapBoot.PingTile, MapPingKind.Default, 0, out var local));
        Assert.Equal(2, board.Visible.Count);
        Assert.Equal(7, board.Visible[0].Id);
        Assert.Equal(8, local.Id);
        Assert.Equal(MapBoot.PingTile, local.Tile);
    }

    [Fact]
    public void Observe_IgnoresDuplicateId()
    {
        var board = new MapPingBoard();
        var ping = new MapPing(1, MapBoot.PingTile, MapPingKind.BuildHere, 4);
        board.Observe(ping);
        board.Observe(ping);
        Assert.Single(board.Visible);
    }
}
