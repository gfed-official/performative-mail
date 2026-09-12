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
}
