using System;

namespace PerformativeMail.Sim.Vehicles;

public readonly record struct VehicleContext(bool OnRoad, VehicleKind Kind = VehicleKind.Bike)
{
    public const float BikeOnRoadMetersPerSecond = 8.0f;
    public const float BikeOffRoadMetersPerSecond = 5.0f;
    public const float MailTruckOnRoadMetersPerSecond = 14.0f;
    public const float MailTruckOffRoadMetersPerSecond = 7.0f;
    public const float SpeedClampFactor = 1.1f;

    public static VehicleContext BikeOnRoad { get; } = new(true, VehicleKind.Bike);

    public static VehicleContext BikeOffRoad { get; } = new(false, VehicleKind.Bike);

    public static VehicleContext MailTruckOnRoad { get; } = new(true, VehicleKind.MailTruck);

    public static VehicleContext MailTruckOffRoad { get; } = new(false, VehicleKind.MailTruck);

    public static VehicleContext OnRoadFor(VehicleKind kind)
    {
        switch (kind)
        {
            case VehicleKind.Bike:
                return BikeOnRoad;
            case VehicleKind.MailTruck:
                return MailTruckOnRoad;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public float MaxSpeedMetersPerSecond
    {
        get
        {
            switch (Kind)
            {
                case VehicleKind.Bike:
                    return OnRoad ? BikeOnRoadMetersPerSecond : BikeOffRoadMetersPerSecond;
                case VehicleKind.MailTruck:
                    return OnRoad ? MailTruckOnRoadMetersPerSecond : MailTruckOffRoadMetersPerSecond;
                default:
                    throw new ArgumentOutOfRangeException(nameof(Kind), Kind, null);
            }
        }
    }
}
