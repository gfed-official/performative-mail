using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public sealed class RouteAnchors
{
    private readonly Dictionary<uint, TileCoord> _addresses = new Dictionary<uint, TileCoord>();
    private readonly Dictionary<byte, TileCoord> _districts = new Dictionary<byte, TileCoord>();
    private readonly Dictionary<uint, TileCoord> _constructs = new Dictionary<uint, TileCoord>();

    public void AddAddress(AddressId address, TileCoord tile)
    {
        _addresses[address.Packed] = tile;
        if (!_districts.ContainsKey(address.District))
            _districts.Add(address.District, tile);
    }

    public void AddDistrict(byte district, TileCoord tile) => _districts[district] = tile;

    public void AddConstruct(EntityId id, TileCoord tile) => _constructs[id.Value] = tile;

    public bool TryResolve(in RouteStop stop, out TileCoord tile)
    {
        switch (stop.Kind)
        {
            case RouteStopKind.Address:
                return _addresses.TryGetValue(stop.Address.Packed, out tile);
            case RouteStopKind.District:
                return _districts.TryGetValue(stop.District, out tile);
            case RouteStopKind.Construct:
                return _constructs.TryGetValue(stop.Construct.Value, out tile);
            default:
                throw new ArgumentOutOfRangeException(nameof(stop), stop.Kind, null);
        }
    }
}
