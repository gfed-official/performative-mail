using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Players;

public sealed class PlayerBody
{
    private uint? _lastProcessed;

    public PlayerBody(EntityId id)
        : this(id, PlayerPose.Origin, 0)
    {
    }

    public PlayerBody(EntityId id, in PlayerPose pose, uint spawnSlot)
    {
        Id = id;
        HpPct = 100;
        Xcm = pose.Xcm;
        Ycm = pose.Ycm;
        Zcm = pose.Zcm;
        Yaw = pose.Yaw;
        SpawnSlot = spawnSlot;
    }

    public EntityId Id { get; }

    public uint SpawnSlot { get; }

    public int Xcm { get; private set; }

    public int Ycm { get; private set; }

    public int Zcm { get; private set; }

    public ushort Yaw { get; private set; }

    public byte Anim { get; }

    public byte HpPct { get; }

    public uint LastProcessedInputTick => _lastProcessed ?? 0;

    public InputCmd LastCmd { get; private set; }

    public uint AppliedCount { get; private set; }

    public bool HasAppliedInput => _lastProcessed.HasValue;

    public PlayerPose Pose => new(Xcm, Ycm, Zcm, Yaw);

    public EntityId VehicleId { get; private set; }

    public void SetPose(in PlayerPose pose)
    {
        Xcm = pose.Xcm;
        Ycm = pose.Ycm;
        Zcm = pose.Zcm;
        Yaw = pose.Yaw;
    }

    public void Mount(EntityId vehicle) => VehicleId = vehicle;

    public void Dismount() => VehicleId = default;

    public void RecordInput(in InputCmd cmd)
    {
        if (HasAppliedInput && cmd.Tick <= LastProcessedInputTick)
            return;

        LastCmd = cmd;
        _lastProcessed = cmd.Tick;
        AppliedCount++;
    }

    public void Apply(in InputCmd cmd)
    {
        if (HasAppliedInput && cmd.Tick <= LastProcessedInputTick)
            return;

        if (VehicleId.Value != 0)
        {
            RecordInput(in cmd);
            return;
        }

        var pose = MovementStep.ApplyTick(Pose, in cmd, MovementContext.Unburdened);
        Xcm = pose.Xcm;
        Ycm = pose.Ycm;
        Zcm = pose.Zcm;
        Yaw = pose.Yaw;
        RecordInput(in cmd);
    }
}
