using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Net.Tests;

public sealed class BikePredictionTests
{
    private static readonly EntityId FirstPlayer = EntityId.FromClassAndCounter(EntityClass.Player, 1);
    private static readonly InputCmd ForwardCmd = new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);

    [Fact]
    public void MountedPredict_UsesVehicleStep_NotMovementStep()
    {
        var prediction = new PredictionState();
        var bikeId = EntityId.FromClassAndCounter(EntityClass.Vehicle, 1);
        prediction.Mount(bikeId, VehicleContext.BikeOnRoad);
        prediction.Predict(ForwardCmd);

        var bike = VehicleStep.ApplyTick(PlayerPose.Origin, ForwardCmd, VehicleContext.BikeOnRoad);
        var walk = MovementStep.ApplyTick(PlayerPose.Origin, ForwardCmd, MovementContext.Unburdened);

        Assert.Equal(bike, prediction.Pose);
        Assert.NotEqual(walk, prediction.Pose);
        Assert.Equal(bikeId, prediction.VehicleId);
    }

    [Fact]
    public void UnmountedPredict_StillUsesMovementStep()
    {
        var prediction = new PredictionState();
        prediction.Predict(ForwardCmd);

        var walk = MovementStep.ApplyTick(PlayerPose.Origin, ForwardCmd, MovementContext.Unburdened);
        Assert.Equal(walk, prediction.Pose);
        Assert.Equal(default, prediction.VehicleId);
    }

    [Fact]
    public void MountedListenHost_SnapshotCarriesVehicleId()
    {
        var (server, client, _) = Boot.CreateListenHost();
        server.TickOnce();
        client.Receive();
        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        var bike = server.World.Vehicles.SpawnBike(body.Pose);
        Assert.True(server.World.TryMount(FirstPlayer, bike.Id));

        var cmd = new InputCmd(0, 0, MovementStep.AxisFull, 0, InputButtons.None);
        client.SubmitInput(in cmd);
        client.TickOnce();
        server.TickOnce();
        server.TickOnce();
        client.Receive();

        Assert.True(server.World.Players.TryGet(FirstPlayer, out body));
        Assert.Equal(bike.Id, body.VehicleId);
        Assert.Equal(EntityClass.Vehicle, body.VehicleId.Class);
        Assert.Equal(27, body.Ycm);

        Assert.True(OwnerSnapshot.TryFrom(client.LastSnapshot!, FirstPlayer, out var owner));
        Assert.Equal(bike.Id, owner.VehicleId);
        Assert.Equal(body.Pose, owner.Pose);
        Assert.Equal(bike.Id, client.Prediction.VehicleId);
        Assert.Equal(body.Pose, client.Prediction.Pose);
    }

    [Fact]
    public void ServerRuntime_MountedPlayer_SnapshotVehicleIdIsClassVehicle()
    {
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A));
        loopback.B.Send(2, WireCodec.Encode(new Hello(Protocol.Hash)));
        server.TickOnce();

        Assert.True(server.World.Players.TryGet(FirstPlayer, out var body));
        var bike = server.World.Vehicles.SpawnBike(body.Pose);
        Assert.True(server.World.TryMount(FirstPlayer, bike.Id));

        server.TickOnce();
        server.TickOnce();

        SnapshotPacket? mounted = null;
        while (loopback.B.Poll(out _, out var payload))
        {
            if (WireCodec.TryDecode(payload, out SnapshotPacket? packet) && packet is not null)
                mounted = packet;
        }

        Assert.NotNull(mounted);
        var player = Assert.Single(mounted!.Players);
        Assert.Equal(EntityClass.Vehicle, player.VehicleId.Class);
        Assert.NotEqual(default, player.VehicleId);
        Assert.Equal(bike.Id, player.VehicleId);
    }

    [Fact]
    public void MountedSnapshot_WireRoundTrip_WritesVehicleId()
    {
        var bikeId = EntityId.FromClassAndCounter(EntityClass.Vehicle, 1);
        var player = new PlayerSnapshot(FirstPlayer, 0, 27, 0, 0, 0, 100, 0, bikeId);
        var packet = new SnapshotPacket(1, new[] { player });
        var bytes = WireCodec.Encode(packet);

        Assert.True(WireCodec.TryDecode(bytes, out SnapshotPacket? decoded));
        Assert.NotNull(decoded);
        var got = Assert.Single(decoded!.Players);
        Assert.Equal(bikeId, got.VehicleId);
        Assert.Equal(27, got.Ycm);
    }

    [Fact]
    public void TwoPlayers_FirstWalkSecondMounted_RoundTrip()
    {
        var bikeId = EntityId.FromClassAndCounter(EntityClass.Vehicle, 1);
        var walk = new PlayerSnapshot(FirstPlayer, 0, 17, 0, 0, 0, 100, 3);
        var ride = new PlayerSnapshot(
            EntityId.FromClassAndCounter(EntityClass.Player, 2),
            0, 27, 0, 0, 0, 100, 3, bikeId);
        var packet = new SnapshotPacket(3, new[] { walk, ride });

        Assert.True(WireCodec.TryDecode(WireCodec.Encode(packet), out SnapshotPacket? decoded));
        Assert.Equal(default, decoded!.Players[0].VehicleId);
        Assert.Equal(17, decoded.Players[0].Ycm);
        Assert.Equal(bikeId, decoded.Players[1].VehicleId);
        Assert.Equal(27, decoded.Players[1].Ycm);
    }
}
