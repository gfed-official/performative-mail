namespace PerformativeMail.Sim.Vehicles;

public readonly record struct VehicleContext(bool OnRoad)
{
    public const float BikeOnRoadMetersPerSecond = 8.0f;
    public const float BikeOffRoadMetersPerSecond = 5.0f;
    public const float SpeedClampFactor = 1.1f;

    public static VehicleContext BikeOnRoad { get; } = new(true);

    public static VehicleContext BikeOffRoad { get; } = new(false);

    public float MaxSpeedMetersPerSecond =>
        OnRoad ? BikeOnRoadMetersPerSecond : BikeOffRoadMetersPerSecond;
}
