using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public interface IRouteConsole
{
    EntityId Id { get; }

    TileCoord Origin { get; }

    TileCoord ParkingZone { get; }

    VehicleRoute Route { get; }

    NpcDriver? Driver { get; }

    void BindDriver(NpcDriver driver);
}
