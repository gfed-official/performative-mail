using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Vehicles;

public enum RouteStopKind : byte
{
    Address,
    District,
    Construct
}

public readonly record struct RouteStop(
    RouteStopKind Kind,
    AddressId Address,
    byte District,
    EntityId Construct)
{
    public static RouteStop ForAddress(AddressId address) =>
        new(RouteStopKind.Address, address, 0, default);

    public static RouteStop ForDistrict(byte district) =>
        new(RouteStopKind.District, default, district, default);

    public static RouteStop ForConstruct(EntityId construct) =>
        new(RouteStopKind.Construct, default, 0, construct);

    public bool Accepts(AddressId address)
    {
        switch (Kind)
        {
            case RouteStopKind.Address:
                return address.Equals(Address);
            case RouteStopKind.District:
                return address.District == District;
            case RouteStopKind.Construct:
                return false;
            default:
            {
                RouteStopKind unseen = Kind;
                throw new ArgumentOutOfRangeException(nameof(Kind), unseen, null);
            }
        }
    }
}
