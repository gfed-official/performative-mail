using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class VehicleStepTests
{
    [Fact]
    public void OnRoadForward_OneTick_IsEightMetresPerSecondThenQuantises()
    {
        var bike = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.BikeOnRoad);

        Assert.Equal(27, PlayerPose.QuantizeCm(8.0 / 30.0));
        Assert.Equal(0, bike.Xcm);
        Assert.Equal(27, bike.Ycm);
        Assert.Equal(0, bike.Zcm);
    }

    [Fact]
    public void OffRoadForward_OneTick_IsFiveMetresPerSecond()
    {
        var bike = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.BikeOffRoad);

        Assert.Equal(17, PlayerPose.QuantizeCm(5.0 / 30.0));
        Assert.Equal(17, bike.Ycm);
    }

    [Fact]
    public void OnRoadForward_DoesNotCallWalkStep()
    {
        var cmd = Forward();
        var bike = VehicleStep.ApplyTick(PlayerPose.Origin, in cmd, VehicleContext.BikeOnRoad);
        var walk = MovementStep.ApplyTick(PlayerPose.Origin, in cmd, MovementContext.Unburdened);

        Assert.Equal(27, bike.Ycm);
        Assert.Equal(17, walk.Ycm);
        Assert.NotEqual(walk, bike);
    }

    [Fact]
    public void Displacement_UsesOnePointOneClamp()
    {
        Assert.Equal(1.1f, VehicleContext.SpeedClampFactor);
        Assert.Equal(8.0f, VehicleContext.BikeOnRoadMetersPerSecond);
        Assert.Equal(5.0f, VehicleContext.BikeOffRoadMetersPerSecond);
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);
}
