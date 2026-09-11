using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class VehicleFuelTests
{
    [Fact]
    public void DrivingFiveMinutes_ConsumesOneOilCan()
    {
        var truck = new VehicleBody(EntityId.FromClassAndCounter(EntityClass.Vehicle, 1), VehicleKind.MailTruck, PlayerPose.Origin);
        truck.Apply(Forward(), VehicleContext.MailTruckOnRoad);
        Assert.True(truck.SpeedMetersPerSecond > 0f);

        int consumed = 0;
        bool ok = truck.StepFuel(VehicleBody.FuelSecondsPerOilCan, () => { consumed++; return true; });

        Assert.True(ok);
        Assert.Equal(1, consumed);
        Assert.False(truck.IsOutOfFuel);
    }

    [Fact]
    public void EmptyTankNoOilCan_HaltsInsteadOfDriving()
    {
        var truck = new VehicleBody(EntityId.FromClassAndCounter(EntityClass.Vehicle, 2), VehicleKind.MailTruck, PlayerPose.Origin);
        truck.Apply(Forward(), VehicleContext.MailTruckOnRoad);
        var movedPose = truck.Pose;
        Assert.NotEqual(PlayerPose.Origin.Ycm, movedPose.Ycm);

        bool ok = truck.StepFuel(VehicleBody.FuelSecondsPerOilCan, () => false);

        Assert.False(ok);
        Assert.True(truck.IsOutOfFuel);

        truck.Apply(Forward(), VehicleContext.MailTruckOnRoad);
        Assert.Equal(movedPose.Xcm, truck.Pose.Xcm);
        Assert.Equal(movedPose.Ycm, truck.Pose.Ycm);
        Assert.Equal(0f, truck.SpeedMetersPerSecond);
    }

    [Fact]
    public void Idempotent_RepeatedStepsAtEmptyTank_StaysHalted()
    {
        var truck = new VehicleBody(EntityId.FromClassAndCounter(EntityClass.Vehicle, 3), VehicleKind.MailTruck, PlayerPose.Origin);
        truck.Apply(Forward(), VehicleContext.MailTruckOnRoad);
        Assert.False(truck.StepFuel(VehicleBody.FuelSecondsPerOilCan, () => false));
        Assert.True(truck.IsOutOfFuel);

        int calls = 0;
        bool ok = truck.StepFuel(50.0, () =>
        {
            calls++;
            return true;
        });

        Assert.False(ok);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void CrossingMultipleIntervals_ConsumesOneOilCanPerInterval()
    {
        var truck = new VehicleBody(EntityId.FromClassAndCounter(EntityClass.Vehicle, 4), VehicleKind.MailTruck, PlayerPose.Origin);
        truck.Apply(Forward(), VehicleContext.MailTruckOnRoad);
        Assert.True(truck.SpeedMetersPerSecond > 0f);

        int consumed = 0;
        bool ok = truck.StepFuel(VehicleBody.FuelSecondsPerOilCan * 2.5, () => { consumed++; return true; });

        Assert.True(ok);
        Assert.Equal(2, consumed);
        Assert.False(truck.IsOutOfFuel);
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);
}
