using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests;

public sealed class PredictionTests
{
    private static readonly EntityId FirstPlayer = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly MovementContext Context = MovementContext.Unburdened;

    [Fact]
    public void ZeroDelay_PredictedPoseEqualsServerPoseAfterQuantise()
    {
        var (server, client, _) = Boot.CreateListenHost();
        var expected = PlayerPose.Origin;

        for (uint tick = 0; tick < 12; tick++)
        {
            var cmd = Forward(tick);
            expected = MovementStep.ApplyTick(in expected, in cmd, in Context);
            DriveZeroDelay(server, client, in cmd);
        }

        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        Assert.Equal(expected, body.Pose);
        Assert.Equal(expected, client.Prediction.Pose);
        Assert.Equal(body.Pose, client.Prediction.Pose);
        Assert.Equal(11u, body.LastProcessedInputTick);
        Assert.True(OwnerSnapshot.TryFrom(client.LastSnapshot!, FirstPlayer, out var owner));
        Assert.Equal(body.LastProcessedInputTick, owner.LastProcessedInputTick);
        Assert.Equal(body.Pose, owner.Pose);
    }

    [Fact]
    public void SnapshotDelayedSixTicks_ReplayMatchesServer()
    {
        var (server, client, _) = Boot.CreateListenHost();
        PlayerPose stalePose = PlayerPose.Origin;
        uint staleProcessed = 0;
        var serverPose = PlayerPose.Origin;

        for (uint tick = 0; tick < 12; tick++)
        {
            var cmd = Forward(tick);
            client.SubmitInput(in cmd);
            client.SendInputs();
            server.TickOnce();

            Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
            serverPose = body.Pose;
            if (tick == 5)
            {
                stalePose = body.Pose;
                staleProcessed = body.LastProcessedInputTick;
            }
        }

        Assert.Equal(5u, staleProcessed);
        Assert.NotEqual(stalePose, serverPose);

        client.Prediction.Reconcile(new OwnerSnapshot(5, stalePose, staleProcessed));
        Assert.Equal(serverPose, client.Prediction.Pose);
        Assert.Equal(6, client.Prediction.PendingCount);
    }

    [Fact]
    public void DroppedInputPacket_AppliesViaThreeCmdWindow()
    {
        var (server, client, _) = Boot.CreateListenHost();

        DriveSend(server, client, send: true);
        DriveSend(server, client, send: true);
        DriveSend(server, client, send: false);
        DriveSend(server, client, send: true);
        client.Receive();

        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        Assert.Equal(4u, body.AppliedCount);
        Assert.Equal(3u, body.LastProcessedInputTick);

        var expected = PlayerPose.Origin;
        for (uint tick = 0; tick < 4; tick++)
        {
            var cmd = Forward(tick);
            expected = MovementStep.ApplyTick(in expected, in cmd, in Context);
        }

        Assert.Equal(expected, body.Pose);
        Assert.Equal(expected, client.Prediction.Pose);
        Assert.True(OwnerSnapshot.TryFrom(client.LastSnapshot!, FirstPlayer, out var owner));
        Assert.Equal(3u, owner.LastProcessedInputTick);
        Assert.Equal(expected, owner.Pose);
    }

    [Fact]
    public void SubmitInput_StampsTickFromServerTickEstimate_AxisOnly()
    {
        var client = new ClientRuntime();
        client.SubmitInput(new InputCmd(99, 3, -5, 12, InputButtons.Sprint));
        client.SubmitInput(new InputCmd(99, 0, MovementStep.AxisFull, 0, InputButtons.None));

        Assert.Equal(2u, client.ServerTickEstimate);
        Assert.Equal(2, client.Prediction.PendingCount);
        Assert.Equal(0u, client.Prediction.Pending[0].Tick);
        Assert.Equal((sbyte)3, client.Prediction.Pending[0].AxisX);
        Assert.Equal((sbyte)-5, client.Prediction.Pending[0].AxisY);
        Assert.Equal(1u, client.Prediction.Pending[1].Tick);
        Assert.Equal(MovementStep.AxisFull, client.Prediction.Pending[1].AxisY);
    }

    private static InputCmd Forward(uint tick) =>
        new(tick, 0, MovementStep.AxisFull, 0, InputButtons.None);

    private static void DriveZeroDelay(ServerRuntime server, ClientRuntime client, in InputCmd cmd)
    {
        client.SubmitInput(in cmd);
        client.TickOnce();
        server.TickOnce();
        client.Receive();
    }

    private static void DriveSend(ServerRuntime server, ClientRuntime client, bool send)
    {
        client.SubmitInput(Forward(0));
        if (send)
            client.SendInputs();
        server.TickOnce();
    }
}
