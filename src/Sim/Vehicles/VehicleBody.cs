using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Vehicles;

public sealed class VehicleBody
{
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
}
