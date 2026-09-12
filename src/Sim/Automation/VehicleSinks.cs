using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

internal static class VehicleSinks
{
    internal static bool TryFindParkedCargo(
        VehicleTable? vehicles,
        int tileCm,
        TileCoord tile,
        out ContainerId cargo,
        IReadOnlyList<SmallPortSite>? ports = null)
    {
        cargo = default;
        if (vehicles is null) return false;
        var all = vehicles.All;
        for (int i = 0; i < all.Count; i++)
        {
            var vehicle = all[i];
            if (!vehicle.IsParked) continue;
            if (vehicle.Cargo is not ContainerId id) continue;
            if (vehicle.Kind == VehicleKind.Motorboat)
            {
                if (!TryDockedPort(ports, vehicle, tileCm, out var port))
                    continue;
                if (!port.LoadingFace.Equals(tile) && !vehicle.LoadingFaceTile(tileCm).Equals(tile))
                    continue;
            }
            else if (!vehicle.LoadingFaceTile(tileCm).Equals(tile))
            {
                continue;
            }

            cargo = id;
            return true;
        }

        return false;
    }

    private static bool TryDockedPort(
        IReadOnlyList<SmallPortSite>? ports,
        VehicleBody vehicle,
        int tileCm,
        out SmallPortSite port)
    {
        port = null!;
        if (ports is null) return false;
        for (int i = 0; i < ports.Count; i++)
        {
            var row = ports[i];
            if (!SmallPort.HoldsParked(row.Origin, row.Facing, vehicle, tileCm))
                continue;
            port = row;
            return true;
        }

        return false;
    }
}
