using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Tests.Inventory;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Sim.Tests.Vehicles;

public sealed class MailTruckTests
{
    [Fact]
    public void SpawnMailTruck_AllocatesEmptyMailCargoGrid()
    {
        var inventory = new InventorySystem(TestStackCatalog.Default);
        var vehicles = new VehicleTable();
        var body = vehicles.SpawnMailTruck(PlayerPose.Origin, inventory);

        Assert.Equal(VehicleKind.MailTruck, body.Kind);
        Assert.NotNull(body.Cargo);
        Assert.True(inventory.TryGetContainer(body.Cargo.Value, out var cargo));
        Assert.Empty(cargo.Entries);
        Assert.Equal(8, cargo.Spec.Shape.Cols);
        Assert.Equal(10, cargo.Spec.Shape.Rows);
    }

    [Fact]
    public void MailTruckContext_OnRoadIsFourteen_OffRoadIsSeven()
    {
        Assert.Equal(14.0f, VehicleContext.MailTruckOnRoad.MaxSpeedMetersPerSecond);
        Assert.Equal(7.0f, VehicleContext.MailTruckOffRoad.MaxSpeedMetersPerSecond);
    }

    [Fact]
    public void OnRoadForward_OneTick_IsFourteenMetresPerSecondThenQuantises()
    {
        var truck = VehicleStep.ApplyTick(
            PlayerPose.Origin,
            Forward(),
            VehicleContext.MailTruckOnRoad);

        Assert.Equal(47, PlayerPose.QuantizeCm(14.0 / 30.0));
        Assert.Equal(0, truck.Xcm);
        Assert.Equal(47, truck.Ycm);
        Assert.Equal(0, truck.Zcm);
    }

    [Fact]
    public void Apply_RecordsQuantisedSpeed_ThenZeroWhenStopped()
    {
        var body = new VehicleBody(
            EntityId.FromClassAndCounter(EntityClass.Vehicle, 1),
            VehicleKind.MailTruck,
            PlayerPose.Origin);

        body.Apply(Forward(), VehicleContext.MailTruckOnRoad);

        var expected = (float)(PlayerPose.QuantizeCm(14.0 / TickClock.TickHz) / 100.0 * TickClock.TickHz);
        Assert.Equal(expected, body.SpeedMetersPerSecond);

        body.Apply(Idle(), VehicleContext.MailTruckOnRoad);
        Assert.Equal(0f, body.SpeedMetersPerSecond);
    }

    private static InputCmd Forward() =>
        new(0, 0, MovementStep.AxisFull, 0, InputButtons.None);

    private static InputCmd Idle() =>
        new(0, 0, 0, 0, InputButtons.None);
}
