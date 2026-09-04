using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Client;

public sealed class PredictionState
{
    private readonly List<InputCmd> _pending = new List<InputCmd>();
    private readonly MovementContext _context;
    private VehicleContext _vehicle = VehicleContext.BikeOnRoad;

    public PredictionState()
        : this(MovementContext.Unburdened)
    {
    }

    public PredictionState(MovementContext context)
    {
        _context = context;
        Pose = PlayerPose.Origin;
    }

    public PlayerPose Pose { get; private set; }

    public EntityId VehicleId { get; private set; }

    public int PendingCount => _pending.Count;

    public IReadOnlyList<InputCmd> Pending => _pending;

    public void Mount(EntityId vehicle, VehicleContext? context = null)
    {
        VehicleId = vehicle;
        if (context.HasValue)
            _vehicle = context.Value;
    }

    public void Dismount() => VehicleId = default;

    public void Predict(in InputCmd cmd)
    {
        Pose = Step(Pose, in cmd);
        _pending.Add(cmd);
    }

    public void Reconcile(in OwnerSnapshot snapshot)
    {
        if (snapshot.VehicleId.Value != 0)
            Mount(snapshot.VehicleId);
        else
            Dismount();
        Reconcile(snapshot.Pose, snapshot.LastProcessedInputTick);
    }

    public void Reconcile(in PlayerPose snapshotPose, uint lastProcessedInputTick)
    {
        Pose = snapshotPose;
        _pending.RemoveAll(cmd => cmd.Tick <= lastProcessedInputTick);
        if (_pending.Count > 1)
            _pending.Sort(CompareTick);

        for (int i = 0; i < _pending.Count; i++)
        {
            var cmd = _pending[i];
            Pose = Step(Pose, in cmd);
        }
    }

    private PlayerPose Step(in PlayerPose pose, in InputCmd cmd) =>
        VehicleId.Value != 0
            ? VehicleStep.ApplyTick(in pose, in cmd, in _vehicle)
            : MovementStep.ApplyTick(in pose, in cmd, in _context);

    private static int CompareTick(InputCmd left, InputCmd right) => left.Tick.CompareTo(right.Tick);
}
