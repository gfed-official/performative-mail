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

public sealed class LaneEventTests
{
    private static readonly EntityId Owner = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly EntityId FirstConstruct = EntityId.FromClassAndCounter(EntityClass.Construct, 1);
    private static readonly SegmentId FirstSegment = new(1);
    private static readonly AddressColour OakSwatch = new(2, 3);

    private static readonly byte[] InsertBytes =
    {
        0x46,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00,
        0x01, 0x00,
        0x02,
        0x03,
        0x00, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] RemoveBytes =
    {
        0x47,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00,
    };

    [Fact]
    public void MessageKind_LaneInsertAndRemove_AreSeventyAndSeventyOne()
    {
        Assert.Equal(70, (byte)MessageKind.LaneInsert);
        Assert.Equal(71, (byte)MessageKind.LaneRemove);
        Assert.Equal(0x4112C9FAu, Protocol.SchemaHash);
    }

    [Fact]
    public void LaneInsert_GoldenRoundTrip()
    {
        var insert = new LaneInsert(FirstSegment, 0, MailKinds.Letter, OakSwatch, 0);
        Assert.Equal(InsertBytes, LaneCodec.Encode(insert));
        Assert.True(LaneCodec.TryDecode(InsertBytes, out LaneInsert decoded));
        Assert.Equal(insert, decoded);
    }

    [Fact]
    public void LaneRemove_GoldenRoundTrip()
    {
        var remove = new LaneRemove(FirstSegment, 0);
        Assert.Equal(RemoveBytes, LaneCodec.Encode(remove));
        Assert.True(LaneCodec.TryDecode(RemoveBytes, out LaneRemove decoded));
        Assert.Equal(remove, decoded);
    }

    [Fact]
    public void LaneInsert_RejectsBadLaneAndNegativePosition()
    {
        var badLane = (byte[])InsertBytes.Clone();
        badLane[9] = 2;
        Assert.False(LaneCodec.TryDecode(badLane, out LaneInsert _));

        var negative = LaneCodec.Encode(new LaneInsert(FirstSegment, 0, MailKinds.Letter, OakSwatch, 0));
        negative[14] = 0xFF;
        negative[15] = 0xFF;
        negative[16] = 0xFF;
        negative[17] = 0xFF;
        Assert.False(LaneCodec.TryDecode(negative, out LaneInsert _));
    }

    [Fact]
    public void LaneInsertThenRemove_TwoClients_LaneCountMatchesServer()
    {
        var fx = Hosted();
        fx.World.Belts.Compile(new[] { EastBelt(new TileCoord(1, 1)) });
        var segment = Assert.Single(fx.World.Belts.Segments);

        Assert.True(segment.TryInsert(0, 11, 0f, MailKinds.Letter, new AddressId(2, 3, 1, 0)));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Single(segment.Lane(0));
        Assert.Empty(segment.Lane(1));
        Assert.Equal(1, fx.First.Lanes.Count(segment.Id, 0));
        Assert.Equal(1, fx.Second.Lanes.Count(segment.Id, 0));
        Assert.Equal(0, fx.First.Lanes.Count(segment.Id, 1));
        Assert.Equal(0, fx.Second.Lanes.Count(segment.Id, 1));

        Assert.True(segment.TryTakeHead(0, out var taken));
        Assert.Equal(11, taken.ItemId);
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Empty(segment.Lane(0));
        Assert.Equal(0, fx.First.Lanes.Count(segment.Id, 0));
        Assert.Equal(0, fx.Second.Lanes.Count(segment.Id, 0));
    }

    [Fact]
    public void RejectedInsert_DoesNotChangeClientCount()
    {
        var fx = Hosted();
        fx.World.Belts.Compile(new[] { EastBelt(new TileCoord(1, 1)) });
        var segment = Assert.Single(fx.World.Belts.Segments);
        Assert.True(segment.TryInsert(0, 1, 0f));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.False(segment.TryInsert(0, 2, 0.4f));
        fx.Server.TickOnce();
        fx.First.Receive();
        fx.Second.Receive();

        Assert.Single(segment.Lane(0));
        Assert.Equal(1, fx.First.Lanes.Count(segment.Id, 0));
        Assert.Equal(1, fx.Second.Lanes.Count(segment.Id, 0));
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
