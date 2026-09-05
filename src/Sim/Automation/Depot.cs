using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public static class Depot
{
    public const string BuildingId = "depot";

    public static ContainerSpec Spec { get; } = new(ContainerShape.Grid(16, 10), null);

    public static TileCoord[] Occupied(TileCoord origin)
        => new[]
        {
            origin,
            new TileCoord(origin.X + 1, origin.Y),
            new TileCoord(origin.X, origin.Y + 1),
            new TileCoord(origin.X + 1, origin.Y + 1)
        };
}
