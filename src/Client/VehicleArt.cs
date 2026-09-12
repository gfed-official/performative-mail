using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Client;

public readonly record struct VehiclePlaceholder(
    float WidthMeters,
    float HeightMeters,
    float LengthMeters,
    float ColorR,
    float ColorG,
    float ColorB);

public static class VehicleArt
{
    public const string Bike = "res://art/props/bike_01.glb";
    public const string MailTruck = "res://art/props/truck_01.glb";

    public static readonly VehiclePlaceholder BikePlaceholder = new(
        0.45f,
        1.05f,
        1.7f,
        0.18f,
        0.23f,
        0.55f);

    public static readonly VehiclePlaceholder MailTruckPlaceholder = new(
        1.85f,
        1.55f,
        4.6f,
        0.72f,
        0.29f,
        0.12f);

    public static string PathForVehicle(VehicleKind kind)
    {
        switch (kind)
        {
            case VehicleKind.Bike:
                return Bike;
            case VehicleKind.MailTruck:
                return MailTruck;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public static VehiclePlaceholder PlaceholderFor(VehicleKind kind)
    {
        switch (kind)
        {
            case VehicleKind.Bike:
                return BikePlaceholder;
            case VehicleKind.MailTruck:
                return MailTruckPlaceholder;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
