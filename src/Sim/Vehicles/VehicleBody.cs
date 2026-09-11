using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Vehicles;

public sealed class VehicleBody
{
    private const float ParkedSpeedThreshold = 0.1f;

    public VehicleBody(EntityId id, VehicleKind kind, in PlayerPose pose, ContainerId? cargo = null)
    {
        Id = id;
        Kind = kind;
        Pose = pose;
        Cargo = cargo;
    }

    public EntityId Id { get; }

    public VehicleKind Kind { get; }

    public PlayerPose Pose { get; private set; }

    public EntityId Driver { get; private set; }

    public ContainerId? Cargo { get; private set; }

    public float SpeedMetersPerSecond { get; private set; }

    public bool IsParked => SpeedMetersPerSecond < ParkedSpeedThreshold;

    public Facing Facing => NearestCardinal(Pose.Yaw);

    public void SetPose(in PlayerPose pose) => Pose = pose;

    public void SetDriver(EntityId driver) => Driver = driver;

    public void ClearDriver() => Driver = default;

    public void Apply(in InputCmd cmd, in VehicleContext context)
    {
        var next = VehicleStep.ApplyTick(Pose, in cmd, in context);
        var dx = (next.Xcm - Pose.Xcm) / 100.0;
        var dy = (next.Ycm - Pose.Ycm) / 100.0;
        var dz = (next.Zcm - Pose.Zcm) / 100.0;
        SpeedMetersPerSecond = (float)(Math.Sqrt(dx * dx + dy * dy + dz * dz) / TickClock.TickDurationSeconds);
        Pose = next;
    }

    public TileCoord Tile(int tileCm) => new TileCoord(FloorDiv(Pose.Xcm, tileCm), FloorDiv(Pose.Ycm, tileCm));

    public TileCoord LoadingFaceTile(int tileCm) => Step(Tile(tileCm), Opposite(Facing));

    private static Facing NearestCardinal(ushort yaw) => (Facing)((int)Math.Round(yaw / 16384.0) & 3);

    private static Facing Opposite(Facing facing) => (Facing)(((int)facing + 2) & 3);

    private static TileCoord Step(TileCoord tile, Facing facing)
    {
        switch (facing)
        {
            case Facing.North: return new TileCoord(tile.X, tile.Y + 1);
            case Facing.East: return new TileCoord(tile.X + 1, tile.Y);
            case Facing.South: return new TileCoord(tile.X, tile.Y - 1);
            case Facing.West: return new TileCoord(tile.X - 1, tile.Y);
            default: throw new ArgumentOutOfRangeException(nameof(facing), facing, null);
        }
    }

    private static int FloorDiv(int value, int divisor)
    {
        int q = value / divisor;
        if ((value < 0) != (divisor < 0) && value % divisor != 0) q--;
        return q;
    }
}
