using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Vehicles;

public sealed class VehicleBody
{
    public VehicleBody(EntityId id, VehicleKind kind, in PlayerPose pose)
    {
        Id = id;
        Kind = kind;
        Pose = pose;
    }

    public EntityId Id { get; }

    public VehicleKind Kind { get; }

    public PlayerPose Pose { get; private set; }

    public EntityId Driver { get; private set; }

    public void SetPose(in PlayerPose pose) => Pose = pose;

    public void SetDriver(EntityId driver) => Driver = driver;

    public void ClearDriver() => Driver = default;

    public void Apply(in InputCmd cmd, in VehicleContext context) =>
        Pose = VehicleStep.ApplyTick(Pose, in cmd, in context);
}
