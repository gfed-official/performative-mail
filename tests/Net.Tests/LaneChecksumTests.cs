using System;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class LaneChecksumTests
{
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly EntityId FirstConstruct = EntityId.FromClassAndCounter(EntityClass.Construct, 1);
    private static readonly SegmentId FirstSegment = new(1);

    private static readonly byte[] StateBytes =
    {
        0x49,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00,
        0x01, 0x00,
        0x0B, 0x00, 0x00, 0x00,
        0xC8, 0x00, 0x00, 0x00,
    };

    [Fact]
    public void MessageKind_ChecksumAndState_AreSeventyTwoAndSeventyThree()
    {
        Assert.Equal(72, (byte)MessageKind.LaneChecksum);
        Assert.Equal(73, (byte)MessageKind.LaneState);
        Assert.Equal(0x4112C9FAu, Protocol.SchemaHash);
    }

    [Fact]
    public void LaneHash_Empty_IsOffset32()
    {
        Assert.Equal(Fnv.Offset32, LaneHash.Of(Array.Empty<int>()));
    }

    [Fact]
    public void LaneHash_Quantize_IsTwentyFiveCm()
    {
        Assert.Equal(25, LaneHash.QuantumCm);
        Assert.Equal(0, LaneHash.Quantize(24));
        Assert.Equal(1, LaneHash.Quantize(25));
        Assert.Equal(8, LaneHash.Quantize(200));
    }

    [Fact]
    public void LaneHash_SameQuantum_Matches_DriftAcrossQuantum_Differs()
    {
        uint atRest = LaneHash.Of(new[] { 0 });
        uint inside = LaneHash.Of(new[] { 24 });
        uint across = LaneHash.Of(new[] { 25 });
        Assert.Equal(atRest, inside);
        Assert.NotEqual(atRest, across);
        Assert.Equal(Fnv.Hash32(new byte[] { 0, 0, 0, 0 }), atRest);
    }

    [Fact]
    public void LaneChecksum_GoldenRoundTrip()
    {
        var checksum = new LaneChecksum(FirstSegment, 0, 1, LaneHash.Of(new[] { 0 }));
        var encoded = LaneCodec.Encode(checksum);
        Assert.Equal(0x48, encoded[0]);
        Assert.True(LaneCodec.TryDecode(encoded, out LaneChecksum decoded));
        Assert.Equal(checksum, decoded);
    }

    [Fact]
    public void LaneState_GoldenRoundTrip()
    {
        var state = new LaneState(FirstSegment, 0, new[] { new LaneStateItem(11, 200) });
        Assert.Equal(StateBytes, LaneCodec.Encode(state));
        Assert.True(LaneCodec.TryDecode(StateBytes, out LaneState decoded));
        Assert.Equal(state.Segment, decoded.Segment);
        Assert.Equal(state.Lane, decoded.Lane);
        Assert.Equal(state.Items, decoded.Items);
    }

    [Fact]
    public void LaneChecksum_RejectsBadLane()
    {
        var bad = (byte[])LaneCodec.Encode(new LaneChecksum(FirstSegment, 0, 0, Fnv.Offset32)).Clone();
        bad[9] = 2;
        Assert.False(LaneCodec.TryDecode(bad, out LaneChecksum _));
    }

    [Fact]
    public void LaneState_RejectsNegativePosition()
    {
        var encoded = LaneCodec.Encode(new LaneState(FirstSegment, 0, new[] { new LaneStateItem(1, 0) }));
        encoded[16] = 0xFF;
        encoded[17] = 0xFF;
        encoded[18] = 0xFF;
        encoded[19] = 0xFF;
        Assert.False(LaneCodec.TryDecode(encoded, out LaneState _));
    }

    [Fact]
    public void MatchingChecksums_EmitNoLaneState()
    {
        var fx = Hosted();
        fx.World.Belts.Compile(new[] { EastBelt(new TileCoord(1, 1)) });
        var segment = Assert.Single(fx.World.Belts.Segments);
        Assert.True(segment.TryInsert(0, 11, 0f, MailKinds.Letter, new AddressId(2, 3, 1, 0)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.True(fx.First.Lanes.Matches(segment.Checksum(0)));
        Assert.True(fx.Second.Lanes.Matches(segment.Checksum(0)));

        WaitChecksum(fx);
        Assert.True(fx.First.LaneChecksumCount >= 2);
        Assert.Equal(0, fx.First.LaneStateCount);
        Assert.Equal(0, fx.Second.LaneStateCount);
        Assert.True(fx.First.Lanes.Matches(segment.Checksum(0)));
        Assert.True(fx.Second.Lanes.Matches(segment.Checksum(1)));
    }

    [Fact]
    public void PlantedPositionDrift_EmitsOneResend()
    {
        var fx = Hosted();
        fx.World.Belts.Compile(new[] { EastBelt(new TileCoord(1, 1)) });
        var segment = Assert.Single(fx.World.Belts.Segments);
        Assert.True(segment.TryInsert(0, 11, 0f, MailKinds.Letter, new AddressId(2, 3, 1, 0)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        WaitChecksum(fx);
        Assert.Equal(0, fx.First.LaneStateCount);
        Assert.True(fx.First.Lanes.Matches(segment.Checksum(0)));

        Assert.True(fx.First.Lanes.TryPlantDrift(segment.Id, 0, LaneHash.QuantumCm));
        Assert.False(fx.First.Lanes.Matches(segment.Checksum(0)));
        Assert.True(fx.Second.Lanes.Matches(segment.Checksum(0)));

        Assert.True(fx.Server.ResendLane(segment.Id, 0));
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Equal(1, fx.First.LaneStateCount);
        Assert.Equal(1, fx.Second.LaneStateCount);
        Assert.True(fx.First.Lanes.Matches(segment.Checksum(0)));
        Assert.Equal(1, fx.First.Lanes.Count(segment.Id, 0));

        WaitChecksum(fx);
        Assert.Equal(1, fx.First.LaneStateCount);
        Assert.True(fx.First.Lanes.Matches(segment.Checksum(0)));
    }

    private static void WaitChecksum(Fixture fx)
    {
        int before = fx.First.LaneChecksumCount;
        int period = TickClock.TicksFromSeconds(2);
        for (int i = 0; i < period; i++)
        {
            fx.Server.TickOnce();
            fx.First.Receive();
            fx.Second.Receive();
            if (fx.First.LaneChecksumCount > before)
                return;
        }

        Assert.Fail($"No LaneChecksum in {period} ticks.");
    }

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
