using System;

namespace PerformativeMail.Sim.Vehicles;

public readonly record struct VehicleContext(
    bool OnRoad,
    VehicleKind Kind = VehicleKind.Bike,
    float SpeedRatio = 1f)
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

    public VehicleContext AtRatio(float speedRatio)
    {
        if (speedRatio < 0f || float.IsNaN(speedRatio) || float.IsInfinity(speedRatio))
            throw new ArgumentOutOfRangeException(nameof(speedRatio), speedRatio, null);
        return this with { SpeedRatio = speedRatio };
    }

    public float MaxSpeedMetersPerSecond => BaseSpeedMetersPerSecond * SpeedRatio;

    private float BaseSpeedMetersPerSecond
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
