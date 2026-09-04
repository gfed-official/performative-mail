using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class LoopbackLinkTests
{
    [Fact]
    public void OverPipes_EmitsOpenedThenData()
    {
        var pair = new LoopbackTransport();
        var link = LoopbackLink.OverPipes(pair.A);

        Assert.True(link.TryPoll(out var opened));
        Assert.Equal(LinkEventKind.Opened, opened.Kind);
        Assert.Equal(ConnectionId.HostSeat, opened.Connection);

        pair.B.Send(2, new byte[] { 9 });
        Assert.True(link.TryPoll(out var data));
        Assert.Equal(LinkEventKind.Data, data.Kind);
        Assert.Equal(2, data.ChannelId);
        Assert.Equal(new byte[] { 9 }, data.Payload);
        Assert.False(link.TryPoll(out _));
    }

    [Fact]
    public void Close_ThenPoll_YieldsClosed_AndSendIsNoOp()
    {
        var pair = new LoopbackTransport();
        var link = LoopbackLink.OverPipes(pair.A);
        Assert.True(link.TryPoll(out _));

        link.Close(ConnectionId.HostSeat, DisconnectReason.PeerLeft);
        Assert.True(link.TryPoll(out var closed));
        Assert.Equal(LinkEventKind.Closed, closed.Kind);
        Assert.Equal(DisconnectReason.PeerLeft, closed.Reason);

        link.Send(ConnectionId.HostSeat, 0, new byte[] { 1 });
        Assert.False(pair.B.Poll(out _, out _));
    }
}

public sealed class TwoClientPresenceTests
{
    [Fact]
    public void TwoClients_Walk_SeeDistinctPawns()
    {
        var hub = LoopbackHub.ForSeats(2);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds));
        var host = new ClientRuntime();
        var guest = new ClientRuntime();
        host.Connect(hub.ClientEnds[0]);
        guest.Connect(hub.ClientEnds[1]);

        server.TickOnce();
        host.Receive();
        guest.Receive();

        Assert.True(host.LocalPlayer.HasValue);
        Assert.True(guest.LocalPlayer.HasValue);
        Assert.NotEqual(host.LocalPlayer.Value, guest.LocalPlayer.Value);
        Assert.Equal(1, host.RemoteCount);
        Assert.Equal(1, guest.RemoteCount);

        var hostSpawn = host.Prediction.Pose;
        var forward = new InputCmd(0, 0, sbyte.MaxValue, 0, InputButtons.None);
        for (int i = 0; i < 30; i++)
        {
            host.SubmitInput(in forward);
            host.TickOnce();
            guest.TickOnce();
            server.TickOnce();
            host.Receive();
            guest.Receive();
        }

        Assert.NotEqual(hostSpawn, host.Prediction.Pose);
        Assert.NotEqual(host.Prediction.Pose, guest.Prediction.Pose);

        var now = InterpolationBuffer.TimeOfTick(guest.LastSnapshot!.ServerTick) + InterpolationBuffer.Holdback;
        Assert.True(guest.TryPresent(host.LocalPlayer.Value, now, out var remote));
        Assert.NotEqual(PlayerPose.Origin, remote);
        Assert.NotEqual(guest.Prediction.Pose, remote);
    }

    [Fact]
    public void CloseGuest_HostSnapshotDropsRemote()
    {
        var hub = LoopbackHub.ForSeats(2);
        var link = LoopbackLink.OverPipes(hub.ServerEnds);
        var server = new ServerRuntime(link);
        var host = new ClientRuntime();
        var guest = new ClientRuntime();
        host.Connect(hub.ClientEnds[0]);
        guest.Connect(hub.ClientEnds[1]);
        server.TickOnce();
        host.Receive();
        guest.Receive();
        Assert.Equal(1, host.RemoteCount);

        link.Close(new ConnectionId(1), DisconnectReason.PeerLeft);
        server.TickOnce();
        server.TickOnce();
        host.Receive();

        Assert.Equal(2, server.World.Players.Count);
        Assert.Equal(1, host.RemoteCount);
    }
}
