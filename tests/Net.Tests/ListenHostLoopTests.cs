using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class ListenHostLoopTests
{
    private static readonly EntityId FirstPlayer = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    [Fact]
    public void Connect_SendsHelloOnChannelTwo()
    {
        var loopback = new LoopbackTransport();
        var client = new ClientRuntime();

        client.Connect(loopback.B);

        Assert.Same(loopback.B, client.Connection);
        Assert.True(loopback.A.Poll(out var channelId, out var payload));
        Assert.Equal(2, channelId);
        Assert.True(WireCodec.TryDecode(payload, out Hello hello));
        Assert.Equal(Protocol.Hash, hello.ProtocolHash);
        Assert.False(loopback.A.Poll(out _, out _));
    }

    [Fact]
    public void SubmitInput_TickOnce_SendsLastThreeNewestFirstOnChannelZero()
    {
        var loopback = new LoopbackTransport();
        var client = new ClientRuntime();
        client.Connect(loopback.B);
        Assert.True(loopback.A.Poll(out _, out _));

        var first = new InputCmd(0, 1, 0, 10, InputButtons.None);
        var second = new InputCmd(1, 0, 1, 20, InputButtons.Sprint);
        var third = new InputCmd(2, -1, 0, 30, InputButtons.Jump);
        var fourth = new InputCmd(3, 0, -1, 40, InputButtons.Interact);

        client.SubmitInput(in first);
        client.SubmitInput(in second);
        client.SubmitInput(in third);
        client.SubmitInput(in fourth);
        client.TickOnce();

        Assert.True(loopback.A.Poll(out var channelId, out var payload));
        Assert.Equal(0, channelId);
        Assert.True(WireCodec.TryDecode(payload, out InputPacket? packet));
        Assert.NotNull(packet);
        Assert.Equal(new[] { fourth, third, second }, packet!.Commands);
        Assert.False(loopback.A.Poll(out _, out _));
    }

    [Fact]
    public void CreateListenHost_ThirtyDrivenTicks_LocalPlayerAndLastSnapshotMatchServer()
    {
        var (server, client, _) = Boot.CreateListenHost();

        for (uint tick = 0; tick < 30; tick++)
            DriveTick(server, client, tick);

        Assert.True(client.LocalPlayer.HasValue);
        Assert.Equal(FirstPlayer, client.LocalPlayer.Value);
        Assert.True(server.World.Players.TryGet(client.LocalPlayer.Value, out var body));
        Assert.Equal(body.Id, client.LocalPlayer.Value);
        Assert.Equal(29u, body.LastProcessedInputTick);

        Assert.NotNull(client.LastSnapshot);
        Assert.Equal(29u, client.LastSnapshot.ServerTick);
        Assert.Equal(20, client.SnapshotCount);

        var snapshotPlayer = Assert.Single(client.LastSnapshot.Players);
        Assert.Equal(FirstPlayer, snapshotPlayer.Id);
        Assert.Equal(29u, snapshotPlayer.LastProcessedInputTick);
        Assert.Equal(0, snapshotPlayer.Xcm);
        Assert.Equal(0, snapshotPlayer.Ycm);
        Assert.Equal(0, snapshotPlayer.Zcm);
    }

    private static void DriveTick(ServerRuntime server, ClientRuntime client, uint tick)
    {
        client.SubmitInput(new InputCmd(tick, 0, 0, 0, InputButtons.None));
        client.TickOnce();
        server.TickOnce();
        client.Receive();
    }
}
