using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Vehicles;

public static class VehicleCargo
{
    public static ContainerSpec Spec { get; } = new(ContainerShape.Grid(8, 10), new[] { StackCategory.Mail });
}
