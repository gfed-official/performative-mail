using System;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Players;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class LaneInterestTests
{
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly EntityId FirstConstruct = EntityId.FromClassAndCounter(EntityClass.Construct, 1);

    [Fact]
    public void OutOfInterest_HasNoSegment()
    {
        var fx = Hosted();
        PlaceFar(fx.World.Players.All[1]);
        var segment = CompileAndInsert(fx);

        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.True(fx.First.Lanes.HasSegment(segment.Id));
        Assert.Equal(1, fx.First.Lanes.Count(segment.Id, 0));
        Assert.False(fx.Second.Lanes.HasSegment(segment.Id));
        Assert.Equal(0, fx.Second.Lanes.Count(segment.Id, 0));
        Assert.Equal(0, fx.Second.LaneStateCount);
    }

    [Fact]
    public void EnterInterest_ReceivesFullLaneState()
    {
        var fx = Hosted();
        PlaceFar(fx.World.Players.All[1]);
        var segment = CompileAndInsert(fx);

        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();
        Assert.False(fx.Second.Lanes.HasSegment(segment.Id));
        int states = fx.Second.LaneStateCount;

        PlaceNear(fx.World.Players.All[1]);
        WaitEnter(fx, segment.Id);

        Assert.True(fx.Second.LaneStateCount > states);
        Assert.True(fx.Second.Lanes.HasSegment(segment.Id));
        Assert.Equal(1, fx.Second.Lanes.Count(segment.Id, 0));
        Assert.True(fx.Second.Lanes.Matches(segment.Checksum(0)));
        Assert.True(fx.Second.Lanes.Matches(segment.Checksum(1)));
    }

    [Fact]
    public void VisualSim_AdvancesAtReplicatedMk1Speed()
    {
        var fx = Hosted();
        var segment = CompileAndInsert(fx, metresFromStart: 0f);

        fx.Server.TickOnce();
        fx.First.Receive();
        Assert.Equal(0, Assert.Single(fx.First.Lanes.Positions(segment.Id, 0)));

        Assert.True(fx.First.Lanes.Advance(segment.Id, 1f, BeltNetwork.Mk1MetresPerSecond));
        Assert.Equal(200, Assert.Single(fx.First.Lanes.Positions(segment.Id, 0)));

        var again = Hosted();
        var other = CompileAndInsert(again, metresFromStart: 0f);
        again.Server.TickOnce();
        again.First.Receive();
        float second = (float)(TickClock.TickDurationSeconds * 30);
        Assert.True(again.First.Lanes.Advance(other.Id, second, BeltNetwork.Mk1MetresPerSecond));
        Assert.Equal(200, Assert.Single(again.First.Lanes.Positions(other.Id, 0)));
    }

    [Fact]
    public void EndpointDraw_WaitsForServerRemove()
    {
        var fx = Hosted();
        fx.World.Belts.Compile(new[] { EastBelt(new TileCoord(1, 1)) });
        var segment = Assert.Single(fx.World.Belts.Segments);
        int lengthCm = BeltNetwork.PositionAtTickCm(segment.LengthMetres);
        Assert.True(segment.TryInsert(0, 11, 0f, MailKinds.Letter, new AddressId(2, 3, 1, 0)));

        fx.Server.TickOnce();
        fx.First.Receive();
        Assert.Equal(0, Assert.Single(fx.First.Lanes.DrawPositions(segment.Id, 0, lengthCm)));

        Assert.True(fx.First.Lanes.Advance(
            segment.Id,
            segment.LengthMetres / BeltNetwork.Mk1MetresPerSecond,
            BeltNetwork.Mk1MetresPerSecond,
            lengthCm));
        Assert.Equal(1, fx.First.Lanes.Count(segment.Id, 0));
        Assert.Empty(fx.First.Lanes.DrawPositions(segment.Id, 0, lengthCm));
        Assert.Equal(lengthCm, Assert.Single(fx.First.Lanes.Positions(segment.Id, 0)));

        Assert.True(segment.TryTakeHead(0, out _));
        fx.Server.TickOnce();
        fx.First.Receive();
        Assert.Equal(0, fx.First.Lanes.Count(segment.Id, 0));
        Assert.Empty(fx.First.Lanes.DrawPositions(segment.Id, 0, lengthCm));
    }

    [Fact]
    public void SegmentInterest_HitsTileAabb()
    {
        var tiles = new[] { new TileCoord(1, 1) };
        Assert.True(SegmentInterest.Hits(300, 300, tiles, 200, 150));
        Assert.False(SegmentInterest.Hits(40000, 0, tiles, 200, 150));
        Assert.True(SegmentInterest.Hits(400 + 15000, 300, tiles, 200, 150));
        Assert.False(SegmentInterest.Hits(400 + 15001, 300, tiles, 200, 150));
    }

    private static void WaitEnter(Fixture fx, SegmentId segment)
    {
        int period = SegmentInterest.PeriodTicks;
        for (int i = 0; i < period + 1; i++)
        {
            fx.Server.TickOnce();
            fx.First.Receive();
            fx.Second.Receive();
            if (fx.Second.Lanes.HasSegment(segment))
                return;
        }

        Assert.Fail($"No LaneState enter in {period} ticks.");
    }

    private static BeltSegment CompileAndInsert(Fixture fx, float metresFromStart = -1f)
    {
        fx.World.Belts.Compile(new[] { EastBelt(new TileCoord(1, 1)) });
        var segment = Assert.Single(fx.World.Belts.Segments);
        float at = metresFromStart < 0f ? 0f : metresFromStart;
        Assert.True(segment.TryInsert(0, 11, at, MailKinds.Letter, new AddressId(2, 3, 1, 0)));
        return segment;
    }

    private static void PlaceFar(PlayerBody body) =>
        body.SetPose(PlayerPose.FromMeters(400, 0, 0, 0));

    private static void PlaceNear(PlayerBody body) =>
        body.SetPose(PlayerPose.Origin);

    private static Fixture Hosted()
    {
        var world = new SimWorld();
        var hub = LoopbackHub.ForSeats(2);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);
        var first = new ClientRuntime();
        var second = new ClientRuntime();
        first.Connect(hub.ClientEnds[0]);
        second.Connect(hub.ClientEnds[1]);
        server.TickOnce();
        first.Receive();
        second.Receive();
        return new Fixture(world, server, first, second);
    }

    private static ConstructRecord EastBelt(TileCoord tile) =>
        new(FirstConstruct, BeltNetwork.BuildingId, tile, Facing.East, Owner, 80, 80);

    private readonly record struct Fixture(
        SimWorld World,
        ServerRuntime Server,
        ClientRuntime First,
        ClientRuntime Second);
}
