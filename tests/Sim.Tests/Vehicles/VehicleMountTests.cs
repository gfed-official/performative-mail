using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class VehicleMountTests
{
    [Fact]
    public void Remount_FreesPreviousBike()
    {
        var world = new SimWorld();
        var player = world.Players.SpawnAtOrigin();
        var first = world.Vehicles.SpawnBike(player.Pose);
        var second = world.Vehicles.SpawnBike(player.Pose);

        Assert.True(world.TryMount(player.Id, first.Id));
        Assert.True(world.TryMount(player.Id, second.Id));

        Assert.Equal(0u, first.Driver.Value);
        Assert.Equal(player.Id, second.Driver);
        Assert.Equal(second.Id, player.VehicleId);
    }

    [Fact]
    public void OccupiedBike_RejectsOtherPlayer()
    {
        var world = new SimWorld();
        var rider = world.Players.SpawnAtOrigin();
        var other = world.Players.SpawnAtOrigin();
        var bike = world.Vehicles.SpawnBike(rider.Pose);

        Assert.True(world.TryMount(rider.Id, bike.Id));
        Assert.False(world.TryMount(other.Id, bike.Id));

        Assert.Equal(rider.Id, bike.Driver);
        Assert.Equal(rider.Id, rider.VehicleId);
        Assert.Equal(0u, other.VehicleId.Value);
    }

    [Fact]
    public void TryDismount_ClearsPlayerAndDriver()
    {
        var world = new SimWorld();
        var player = world.Players.SpawnAtOrigin();
        var bike = world.Vehicles.SpawnBike(player.Pose);

        Assert.True(world.TryMount(player.Id, bike.Id));
        Assert.True(world.TryDismount(player.Id));

        Assert.Equal(0u, player.VehicleId.Value);
        Assert.Equal(0u, bike.Driver.Value);
        Assert.False(world.TryDismount(player.Id));
    }
}
