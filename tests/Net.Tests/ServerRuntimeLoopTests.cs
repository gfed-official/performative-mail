using System.Collections.Generic;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class ServerRuntimeLoopTests
{
    private static readonly EntityId FirstPlayer = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    private static readonly uint[] ExpectedSnapshotTicks =
    {
        0, 2, 3, 5, 6, 8, 9, 11, 12, 14, 15, 17, 18, 20, 21, 23, 24, 26, 27, 29,
    };

    [Fact]
    public void ProtocolMismatch_Rejects_AndDoesNotSpawn()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(loopback.A);

        loopback.B.Send(2, WireCodec.Encode(new Hello(Protocol.Hash ^ 0xFFFFFFFFu)));
        server.TickOnce();

        Assert.Equal(0, server.World.Players.Count);
        Assert.False(server.World.Players.TryGet(FirstPlayer, out _));

        var sawReject = false;
        var sawOk = false;
        while (loopback.B.Poll(out var channelId, out var payload))
        {
            if (WireCodec.TryDecode(payload, out HelloReject reject))
            {
                sawReject = true;
                Assert.Equal(2, channelId);
                Assert.Equal(HelloRejectReason.ProtocolMismatch, reject.Reason);
            }

            if (WireCodec.TryDecode(payload, out HelloOk _))
                sawOk = true;
        }

        Assert.True(sawReject);
        Assert.False(sawOk);
    }

    [Fact]
    public void MatchingHello_SpawnsClass1Entity_OnFirstJoin()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(loopback.A);

        loopback.B.Send(2, WireCodec.Encode(new Hello(Protocol.Hash)));
        server.TickOnce();

        Assert.Equal(1, server.World.Players.Count);
        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        Assert.Equal(16777217u, body.Id.Value);
        Assert.Equal(EntityClass.Player, body.Id.Class);
        Assert.Equal(1u, body.Id.Counter);
        Assert.Equal(0, body.Xcm);
        Assert.Equal(0, body.Ycm);
        Assert.Equal(0, body.Zcm);

        var sawOk = false;
        while (loopback.B.Poll(out var channelId, out var payload))
        {
            if (!WireCodec.TryDecode(payload, out HelloOk ok))
                continue;

            sawOk = true;
            Assert.Equal(2, channelId);
            Assert.Equal(FirstPlayer, ok.LocalPlayer);
            Assert.Equal(0u, ok.StartTick);
        }

        Assert.True(sawOk);
    }

    [Fact]
    public void DuplicateCmds_InThreeDeepWindow_ApplyOnce()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(loopback.A);
        loopback.B.Send(2, WireCodec.Encode(new Hello(Protocol.Hash)));

        var cmd = new InputCmd(0, 1, -1, 100, InputButtons.Sprint);
        loopback.B.Send(0, WireCodec.Encode(new InputPacket(new[] { cmd, cmd, cmd })));
        server.TickOnce();

        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        Assert.Equal(1u, body.AppliedCount);
        Assert.Equal(0u, body.LastProcessedInputTick);
        Assert.Equal(cmd, body.LastCmd);
        Assert.Equal(0, body.Xcm);
        Assert.Equal(0, body.Ycm);
        Assert.Equal(0, body.Zcm);

        loopback.B.Send(0, WireCodec.Encode(new InputPacket(new[] { cmd, cmd, cmd })));
        server.TickOnce();

        Assert.Equal(1u, body.AppliedCount);
        Assert.Equal(0u, body.LastProcessedInputTick);
    }

    [Fact]
    public void ThirtyTickOnce_LastProcessedAndSnapshotCadence()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(loopback.A);
        loopback.B.Send(2, WireCodec.Encode(new Hello(Protocol.Hash)));

        var snapshots = new List<SnapshotPacket>();
        for (uint tick = 0; tick < 30; tick++)
        {
            loopback.B.Send(0, WireCodec.Encode(new InputPacket(WindowFor(tick))));
            server.TickOnce();
            DrainSnapshots(loopback.B, snapshots);
        }

        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        Assert.Equal(29u, body.LastProcessedInputTick);
        Assert.Equal(30u, body.AppliedCount);
        Assert.Equal(20, snapshots.Count);
        Assert.Equal(ExpectedSnapshotTicks, SnapshotTicks(snapshots));

        var last = snapshots[snapshots.Count - 1];
        Assert.Equal(29u, last.ServerTick);
        var snapshotPlayer = Assert.Single(last.Players);
        Assert.Equal(FirstPlayer, snapshotPlayer.Id);
        Assert.Equal(0, snapshotPlayer.Xcm);
        Assert.Equal(0, snapshotPlayer.Ycm);
        Assert.Equal(0, snapshotPlayer.Zcm);
        Assert.Equal(29u, snapshotPlayer.LastProcessedInputTick);
    }

    [Fact]
    public void SnapshotCadence_ShouldSend_IsTickMod3NotOne()
    {
        for (uint tick = 0; tick < 30; tick++)
            Assert.Equal(tick % 3 != 1, SnapshotCadence.ShouldSend(tick));
    }

    private static InputCmd[] WindowFor(uint tick)
    {
        var count = tick >= 2 ? 3 : (int)tick + 1;
        var cmds = new InputCmd[count];
        for (int i = 0; i < count; i++)
        {
            var cmdTick = tick - (uint)i;
            cmds[i] = new InputCmd(cmdTick, 0, 0, 0, InputButtons.None);
        }

        return cmds;
    }

    private static void DrainSnapshots(ITransport transport, List<SnapshotPacket> snapshots)
    {
        while (transport.Poll(out _, out var payload))
        {
            if (WireCodec.TryDecode(payload, out SnapshotPacket? packet) && packet is not null)
                snapshots.Add(packet);
        }
    }

    private static uint[] SnapshotTicks(IReadOnlyList<SnapshotPacket> snapshots)
    {
        var ticks = new uint[snapshots.Count];
        for (int i = 0; i < snapshots.Count; i++)
            ticks[i] = snapshots[i].ServerTick;
        return ticks;
    }
}
