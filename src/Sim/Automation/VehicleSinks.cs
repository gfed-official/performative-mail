using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

internal static class VehicleSinks
{
    internal static bool TryFindParkedCargo(VehicleTable? vehicles, int tileCm, TileCoord tile, out ContainerId cargo)
    {
        cargo = default;
        if (vehicles is null) return false;
        var all = vehicles.All;
        for (int i = 0; i < all.Count; i++)
        {
            var vehicle = all[i];
            if (!vehicle.IsParked) continue;
            if (vehicle.Cargo is not ContainerId id) continue;
            if (!vehicle.LoadingFaceTile(tileCm).Equals(tile)) continue;
            cargo = id;
            return true;
        }

        return false;
    }
}
