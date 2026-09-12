using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class MapPingEventTests
{
    private static readonly TileCoord FirstTile = new(5, 6);
    private static readonly TileCoord SecondTile = new(1, 1);

    private static readonly byte[] RequestBytes =
    {
        0x50,
        0x05, 0x00, 0x00, 0x00,
        0x06, 0x00, 0x00, 0x00,
        0x03,
    };

    private static readonly byte[] EventBytes =
    {
        0x51,
        0x01, 0x00, 0x00, 0x00,
        0x05, 0x00, 0x00, 0x00,
        0x06, 0x00, 0x00, 0x00,
        0x03,
        0x00, 0x00, 0x00, 0x00,
    };

    [Fact]
    public void MessageKind_MapPing_IsEightyAndEightyOne()
    {
        Assert.Equal(80, (byte)MessageKind.MapPing);
        Assert.Equal(81, (byte)MessageKind.MapPingEvent);
        Assert.Equal(1, MapPingLimits.RateLimitSeconds);
        Assert.Equal(10, MapPingLimits.LifetimeSeconds);
        Assert.Equal(30, MapPingLimits.RateLimitTicks);
        Assert.Equal(0x4112C9FAu, Protocol.SchemaHash);
    }

    [Fact]
    public void MapPing_GoldenRoundTrip()
    {
        var request = new MapPingRequest(FirstTile.X, FirstTile.Y, (byte)MapPingKind.Danger);
        Assert.Equal(RequestBytes, MapPingCodec.Encode(request));
        Assert.True(MapPingCodec.TryDecode(RequestBytes, out MapPingRequest decoded));
        Assert.Equal(request, decoded);

        var ev = new MapPingEvent(1, FirstTile.X, FirstTile.Y, (byte)MapPingKind.Danger, 0);
        Assert.Equal(EventBytes, MapPingCodec.Encode(ev));
        Assert.True(MapPingCodec.TryDecode(EventBytes, out MapPingEvent seen));
        Assert.Equal(ev, seen);
    }

    [Fact]
    public void MapPing_TwoClients_SecondSeesPlacedPing()
    {
        var fx = Hosted();

        fx.First.SendMapPing(FirstTile, MapPingKind.Danger);
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        var first = Assert.Single(fx.First.Pings.Visible);
        var second = Assert.Single(fx.Second.Pings.Visible);
        Assert.Equal(first, second);
        Assert.Equal(FirstTile, second.Tile);
        Assert.Equal(MapPingKind.Danger, second.Kind);

        var frame = MapFrame.From(
            MapBoot.Tables(),
            null,
            MapFrame.DefaultLayers,
            MapFilter.None,
            fx.Second.Pings.Visible);
        Assert.Equal("danger", Assert.Single(frame.Pings).Key);
        Assert.Equal(FirstTile, frame.Pings[0].Tile);
    }

    [Fact]
    public void MapPing_SecondInsideOneSecond_IsDroppedForBothClients()
    {
        var fx = Hosted();

        fx.First.SendMapPing(FirstTile, MapPingKind.Default);
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();
        Assert.Single(fx.Second.Pings.Visible);

        fx.First.SendMapPing(SecondTile, MapPingKind.NeedMaterials);
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Single(fx.First.Pings.Visible);
        Assert.Single(fx.Second.Pings.Visible);
        Assert.Equal(FirstTile, fx.Second.Pings.Visible[0].Tile);
        Assert.Equal(MapPingKind.Default, fx.Second.Pings.Visible[0].Kind);
    }

    [Fact]
    public void MapPing_AfterOneSecond_SecondPingIsVisible()
    {
        var fx = Hosted();

        fx.First.SendMapPing(FirstTile, MapPingKind.DeliverHere);
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        for (int i = 0; i < MapPingLimits.RateLimitTicks; i++)
            fx.Server.TickOnce();

        fx.First.SendMapPing(SecondTile, MapPingKind.BuildHere);
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Equal(2, fx.First.Pings.Visible.Count);
        Assert.Equal(2, fx.Second.Pings.Visible.Count);
        Assert.Equal(SecondTile, fx.Second.Pings.Visible[1].Tile);
        Assert.Equal(MapPingKind.BuildHere, fx.Second.Pings.Visible[1].Kind);
    }

    [Fact]
    public void MapPing_InvalidKind_DoesNotReachOtherClient()
    {
        var fx = Hosted();

        fx.First.Connection!.Send(
            NetChannels.Reliable,
            MapPingCodec.Encode(new MapPingRequest(FirstTile.X, FirstTile.Y, 99)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Empty(fx.First.Pings.Visible);
        Assert.Empty(fx.Second.Pings.Visible);
    }

    [Fact]
    public void HostAndGuest_TryPlacePing_GuestSeesPingAndRateLimitDropsSecond()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        var idle = MoveIntent.Idle;

        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, in idle, 8);

        Assert.True(host.TryPlacePing(FirstTile, MapPingKind.Danger));
        Assert.False(host.TryPlacePing(SecondTile, MapPingKind.Default));
        PumpBoth(host, guest, ref now, in idle, 2);

        var hostPlay = Assert.IsType<PlaySession.Playing>(host.State);
        var guestPlay = Assert.IsType<PlaySession.Playing>(guest.State);
        var hostPing = Assert.Single(hostPlay.Pings);
        var guestPing = Assert.Single(guestPlay.Pings);
        Assert.Equal(FirstTile, hostPing.Tile);
        Assert.Equal(hostPing, guestPing);
        Assert.Equal(MapPingKind.Danger, guestPing.Kind);
    }

    private static Fixture Hosted()
    {
        var hub = LoopbackHub.ForSeats(2);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds));
        var first = new ClientRuntime();
        var second = new ClientRuntime();
        first.Connect(hub.ClientEnds[0]);
        second.Connect(hub.ClientEnds[1]);
        server.TickOnce();
        first.Receive();
        second.Receive();
        Assert.True(first.LocalPlayer.HasValue);
        Assert.True(second.LocalPlayer.HasValue);
        return new Fixture(server, first, second);
    }

    private static void PumpBoth(
        PlaySessionMachine host,
        PlaySessionMachine guest,
        ref TimeSpan now,
        in MoveIntent intent,
        int steps)
    {
        var tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);
        for (int i = 0; i < steps; i++)
        {
            now += tick;
            host.Pump(now, in intent);
            guest.Pump(now, in intent);
        }
    }

    private readonly record struct Fixture(ServerRuntime Server, ClientRuntime First, ClientRuntime Second);
}
