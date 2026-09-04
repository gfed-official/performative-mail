using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Vehicles;

public sealed class VehicleTable
{
    private readonly Dictionary<uint, VehicleBody> _byId = new Dictionary<uint, VehicleBody>();
    private readonly List<VehicleBody> _order = new List<VehicleBody>();
    private uint _nextCounter = 1;

    public int Count => _order.Count;

    public IReadOnlyList<VehicleBody> All => _order;

    public VehicleBody SpawnBike(in PlayerPose pose)
    {
        var body = new VehicleBody(
            EntityId.FromClassAndCounter(EntityClass.Vehicle, _nextCounter++),
            VehicleKind.Bike,
            in pose);
        _byId.Add(body.Id.Value, body);
        _order.Add(body);
        return body;
    }

    public bool TryGet(EntityId id, out VehicleBody body) => _byId.TryGetValue(id.Value, out body);
}
