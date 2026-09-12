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
    public const string Rowboat = "res://art/props/rowboat_01.glb";
    public const string Motorboat = "res://art/props/motorboat_01.glb";

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

    public static readonly VehiclePlaceholder RowboatPlaceholder = new(
        0.85f,
        0.45f,
        2.4f,
        0.42f,
        0.28f,
        0.16f);

    public static readonly VehiclePlaceholder MotorboatPlaceholder = new(
        1.8f,
        1.4f,
        5.5f,
        0.93f,
        0.94f,
        0.95f);

    public static string PathForVehicle(VehicleKind kind)
    {
        switch (kind)
        {
            case VehicleKind.Bike:
                return Bike;
            case VehicleKind.MailTruck:
                return MailTruck;
            case VehicleKind.Rowboat:
                return Rowboat;
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
            case VehicleKind.Rowboat:
                return RowboatPlaceholder;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
