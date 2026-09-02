using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakJoinTests
{
    [Fact]
    public void Create_ThreeRealSeats_Throws()
    {
        var seats = DummySeats(realCount: 3, botCount: 5);

        var ex = Assert.Throws<ArgumentException>(() => SoakRoster.Create(seats));
        Assert.Contains("2 Real and 6 Bot", ex.Message);
    }

    [Fact]
    public void ForSeats_PairsAreIndependent()
    {
        var hub = LoopbackHub.ForSeats(2);
        Assert.Equal(2, hub.ServerEnds.Count);
        Assert.Equal(2, hub.ClientEnds.Count);

        hub.ClientEnds[0].Send(0, new byte[] { 1 });
        Assert.True(hub.ServerEnds[0].Poll(out var channelId, out var payload));
        Assert.Equal(0, channelId);
        Assert.Equal(new byte[] { 1 }, payload);
        Assert.False(hub.ServerEnds[1].Poll(out _, out _));
    }

    [Fact]
    public void EightHellos_SpawnEightClass1Players_AndStayLiveForThirtyTicks()
    {
        var hub = LoopbackHub.ForSeats(SoakRoster.SeatCount);
        var server = new ServerRuntime(hub.ServerEnds[0]);
        for (int i = 1; i < hub.ServerEnds.Count; i++)
            server.Attach(hub.ServerEnds[i]);

        var seats = new SoakSeat[SoakRoster.SeatCount];
        for (int i = 0; i < seats.Length; i++)
        {
            var kind = i < SoakRoster.RealCount ? SeatKind.Real : SeatKind.Bot;
            var client = new ClientRuntime();
            client.Connect(hub.ClientEnds[i]);
            seats[i] = new SoakSeat(new ConnectionId((byte)i), kind, client, hub.ClientEnds[i]);
        }

        var roster = SoakRoster.Create(seats);

        for (uint tick = 0; tick < 30; tick++)
            DriveTick(server, roster, tick);

        Assert.Equal(SoakRoster.SeatCount, server.World.Players.Count);

        for (int i = 0; i < roster.Seats.Count; i++)
        {
            var seat = roster.Seats[i];
            var expected = EntityId.FromClassAndCounter(EntityClass.Player, (uint)(i + 1));
            Assert.NotNull(seat.Client.Connection);
            Assert.True(seat.Client.LocalPlayer.HasValue);
            Assert.Equal(expected, seat.Client.LocalPlayer.Value);
            Assert.Equal(0u, seat.Client.StartTick);
            Assert.True(server.World.Players.TryGet(expected, out var body));
            Assert.Equal(EntityClass.Player, body.Id.Class);
            Assert.Equal((uint)(i + 1), body.Id.Counter);
            seat.Player = seat.Client.LocalPlayer.Value;
            Assert.NotNull(seat.Client.LastSnapshot);
            Assert.Equal(SoakRoster.SeatCount, seat.Client.LastSnapshot.Players.Count);
        }
    }

    private static void DriveTick(ServerRuntime server, SoakRoster roster, uint tick)
    {
        for (int i = 0; i < roster.Seats.Count; i++)
        {
            var client = roster.Seats[i].Client;
            client.SubmitInput(new InputCmd(tick, 0, 0, 0, InputButtons.None));
            client.TickOnce();
        }

        server.TickOnce();

        for (int i = 0; i < roster.Seats.Count; i++)
            roster.Seats[i].Client.Receive();
    }

    private static SoakSeat[] DummySeats(int realCount, int botCount)
    {
        var hub = LoopbackHub.ForSeats(realCount + botCount);
        var seats = new SoakSeat[realCount + botCount];
        for (int i = 0; i < seats.Length; i++)
        {
            var kind = i < realCount ? SeatKind.Real : SeatKind.Bot;
            seats[i] = new SoakSeat(new ConnectionId((byte)i), kind, new ClientRuntime(), hub.ClientEnds[i]);
        }

        return seats;
    }
}
