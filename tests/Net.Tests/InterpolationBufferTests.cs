using System;
using PerformativeMail.App;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class InterpolationBufferTests
{
    private static readonly EntityId OwnerId = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly EntityId RemoteId = EntityId.FromClassAndCounter(EntityClass.Player, 2);

    [Fact]
    public void TwoSnapshotsFiftyMsApart_SampleAtHundredMsHoldback_LiesOnSegment()
    {
        var buffer = new InterpolationBuffer();
        var start = new PlayerPose(0, 20, -10, 0);
        var end = new PlayerPose(100, 20, -10, 0);
        buffer.Push(TimeSpan.Zero, in start);
        buffer.Push(TimeSpan.FromMilliseconds(50), in end);

        var now = TimeSpan.FromMilliseconds(125);
        Assert.Equal(TimeSpan.FromMilliseconds(100), InterpolationBuffer.Holdback);
        Assert.Equal(TimeSpan.FromMilliseconds(25), now - InterpolationBuffer.Holdback);
        Assert.True(buffer.TryPresent(now, out var pose));
        Assert.True(OnSegment(start, end, pose));
        Assert.Equal(50, pose.Xcm);
        Assert.Equal(20, pose.Ycm);
        Assert.Equal(-10, pose.Zcm);
    }

    [Fact]
    public void ForRemote_OwnerEntity_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => InterpolationBuffer.ForRemote(OwnerId, OwnerId));
    }

    [Fact]
    public void RemoteSnapshotFrom_OwnerEntity_Throws()
    {
        var ownerPlayer = new PlayerSnapshot(OwnerId, 0, 0, 0, 0, 0, 100, 4);
        Assert.Throws<InvalidOperationException>(() => RemoteSnapshot.From(in ownerPlayer, 0, OwnerId));
    }

    [Fact]
    public void ClientRuntime_WiresRemoteThroughRemoteInterpolated_OwnerStaysPredicted()
    {
        var (server, client, transport) = Boot.CreateListenHost();
        server.TickOnce();
        client.Receive();

        Assert.NotNull(client.LocalPlayer);
        var owner = client.LocalPlayer!.Value;
        var remotePose = new PlayerPose(80, 0, 0, 0);
        var packet = new SnapshotPacket(
            0,
            new[]
            {
                new PlayerSnapshot(owner, 0, 0, 0, 0, 0, 100, 0),
                new PlayerSnapshot(RemoteId, 80, 0, 0, 0, 0, 100, 0),
            });

        transport.A.Send(0, WireCodec.Encode(packet));
        client.Receive();

        Assert.Equal(1, client.RemoteCount);
        Assert.True(client.TryGetRemote(RemoteId, out var remote));
        Assert.False(client.TryGetRemote(owner, out _));
        Assert.Same(client.Prediction, client.Owner.State);
        Assert.True(remote.Buffer.TryPresent(InterpolationBuffer.Holdback, out var presented));
        Assert.Equal(remotePose, presented);

        Assert.True(client.TryPresent(owner, TimeSpan.FromMilliseconds(125), out var ownerPose));
        Assert.Equal(client.Prediction.Pose, ownerPose);
        Assert.True(client.TryPresent(RemoteId, InterpolationBuffer.Holdback, out var remotePresented));
        Assert.Equal(remotePose, remotePresented);
    }

    private static bool OnSegment(PlayerPose start, PlayerPose end, PlayerPose point)
    {
        int abx = end.Xcm - start.Xcm;
        int aby = end.Ycm - start.Ycm;
        int abz = end.Zcm - start.Zcm;
        int apx = point.Xcm - start.Xcm;
        int apy = point.Ycm - start.Ycm;
        int apz = point.Zcm - start.Zcm;
        int cx = aby * apz - abz * apy;
        int cy = abz * apx - abx * apz;
        int cz = abx * apy - aby * apx;
        if (cx != 0 || cy != 0 || cz != 0)
            return false;

        int dot = apx * abx + apy * aby + apz * abz;
        int lengthSquared = abx * abx + aby * aby + abz * abz;
        return dot >= 0 && dot <= lengthSquared;
    }
}
