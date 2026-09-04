using System;
using PerformativeMail.App;
using PerformativeMail.Client;
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
    public void Push_OwnerSnapshot_Throws()
    {
        var buffer = new InterpolationBuffer();
        var owner = new OwnerSnapshot(0, PlayerPose.Origin, 0);
        Assert.Throws<InvalidOperationException>(() => buffer.Push(in owner));
        Assert.Equal(0, buffer.Count);
    }

    [Fact]
    public void RemoteSnapshotFrom_OwnerEntity_Throws()
    {
        var ownerPlayer = new PlayerSnapshot(OwnerId, 0, 0, 0, 0, 0, 100, 4);
        Assert.Throws<InvalidOperationException>(() => RemoteSnapshot.From(in ownerPlayer, 0, OwnerId));
    }

    [Fact]
    public void FourthSnapshot_DropsOldest_KeepsLastThree()
    {
        var buffer = new InterpolationBuffer();
        Assert.Equal(3, InterpolationBuffer.Capacity);
        buffer.Push(TimeSpan.FromMilliseconds(0), new PlayerPose(0, 0, 0, 0));
        buffer.Push(TimeSpan.FromMilliseconds(50), new PlayerPose(100, 0, 0, 0));
        buffer.Push(TimeSpan.FromMilliseconds(100), new PlayerPose(200, 0, 0, 0));
        buffer.Push(TimeSpan.FromMilliseconds(150), new PlayerPose(300, 0, 0, 0));
        Assert.Equal(3, buffer.Count);

        // now=175, present=75: oldest (t=0) is gone; sample is on the 50–100 ms segment.
        Assert.True(buffer.TryPresent(TimeSpan.FromMilliseconds(175), out var pose));
        Assert.True(OnSegment(new PlayerPose(100, 0, 0, 0), new PlayerPose(200, 0, 0, 0), pose));
        Assert.Equal(150, pose.Xcm);
    }

    [Fact]
    public void PastNewestWithinExtrapolation_ContinuesAlongLastSegmentVelocity()
    {
        var buffer = new InterpolationBuffer();
        buffer.Push(TimeSpan.Zero, new PlayerPose(0, 0, 0, 0));
        buffer.Push(TimeSpan.FromMilliseconds(50), new PlayerPose(100, 0, 0, 0));

        // present = now - Holdback = 75 ms: 25 ms past newest. Velocity 2 cm/ms → x = 150.
        var now = InterpolationBuffer.Holdback + TimeSpan.FromMilliseconds(75);
        Assert.True(buffer.TryPresent(now, out var pose));
        Assert.Equal(150, pose.Xcm);
    }

    [Fact]
    public void PastMaxExtrapolation_ClampsToNewest()
    {
        var buffer = new InterpolationBuffer();
        buffer.Push(TimeSpan.Zero, new PlayerPose(0, 0, 0, 0));
        buffer.Push(TimeSpan.FromMilliseconds(50), new PlayerPose(100, 0, 0, 0));

        // present = newest + MaxExtrapolation + 1 ms. Beyond the extrapolation window → clamp.
        var now = InterpolationBuffer.Holdback
            + TimeSpan.FromMilliseconds(50)
            + InterpolationBuffer.MaxExtrapolation
            + TimeSpan.FromMilliseconds(1);
        Assert.True(buffer.TryPresent(now, out var pose));
        Assert.Equal(100, pose.Xcm);
    }

    [Fact]
    public void SnapshotGaps_FrameToFrameDeltaStaysNearConstantVelocity()
    {
        // Unreliable 20 Hz snapshots; after the first pair, keep only every third packet (150 ms gaps).
        // Holdback is 100 ms, so present overruns newest and clamp-past-newest freezes then jumps.
        var buffer = new InterpolationBuffer();
        const double VelCmPerMs = 2.0;
        const double FrameMs = 1000.0 / 60.0;
        const int SnapMs = 50;
        var wall = TimeSpan.Zero;
        int lastSnapIndex = -1;
        int? prevX = null;
        double maxAbsError = 0;

        for (int frame = 0; frame < 180; frame++)
        {
            wall += TimeSpan.FromMilliseconds(FrameMs);
            int snapIndex = (int)(wall.TotalMilliseconds / SnapMs);
            if (snapIndex != lastSnapIndex)
            {
                lastSnapIndex = snapIndex;
                if (snapIndex < 2 || snapIndex % 3 == 0)
                {
                    int snapAt = snapIndex * SnapMs;
                    var poseAtSnap = new PlayerPose((int)Math.Round(VelCmPerMs * snapAt), 0, 0, 0);
                    buffer.Push(TimeSpan.FromMilliseconds(snapAt), in poseAtSnap);
                }
            }

            if (wall < InterpolationBuffer.Holdback)
                continue;
            if (!buffer.TryPresent(wall, out var presented))
                continue;

            if (prevX is int last)
            {
                double delta = presented.Xcm - last;
                double ideal = VelCmPerMs * FrameMs;
                maxAbsError = Math.Max(maxAbsError, Math.Abs(delta - ideal));
            }

            prevX = presented.Xcm;
        }

        Assert.True(prevX.HasValue, "Expected presented samples after holdback.");
        Assert.True(
            maxAbsError <= 4.0,
            $"Frame delta drifted from constant velocity by {maxAbsError:F2} cm (limit 4 cm).");
    }

    [Fact]
    public void ClientRuntime_WiresRemoteThroughRemoteInterpolated_OwnerStaysPredicted()
    {
        var (server, client, transport) = Boot.CreateListenHost();
        server.TickOnce();
        client.Receive();

        Assert.NotNull(client.LocalPlayer);
        var owner = client.LocalPlayer!.Value;
        Assert.Equal(0, client.RemoteCount);
        Assert.IsType<PlayerReplication.OwnerPredicted>(client.ReplicationFor(owner));

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
        Assert.IsType<PlayerReplication.OwnerPredicted>(client.ReplicationFor(owner));
        Assert.IsType<PlayerReplication.RemoteInterpolated>(client.ReplicationFor(RemoteId));
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
