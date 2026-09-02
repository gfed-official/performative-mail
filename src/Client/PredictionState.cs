using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Client;

public sealed class PredictionState
{
    private readonly List<InputCmd> _pending = new List<InputCmd>();
    private readonly MovementContext _context;

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

    public int PendingCount => _pending.Count;

    public IReadOnlyList<InputCmd> Pending => _pending;

    public void Predict(in InputCmd cmd)
    {
        Pose = MovementStep.ApplyTick(in Pose, in cmd, in _context);
        _pending.Add(cmd);
    }

    public void Reconcile(in OwnerSnapshot snapshot) =>
        Reconcile(snapshot.Pose, snapshot.LastProcessedInputTick);

    public void Reconcile(in PlayerPose snapshotPose, uint lastProcessedInputTick)
    {
        Pose = snapshotPose;
        _pending.RemoveAll(static cmd => cmd.Tick <= lastProcessedInputTick);
        if (_pending.Count > 1)
            _pending.Sort(CompareTick);

        for (int i = 0; i < _pending.Count; i++)
        {
            var cmd = _pending[i];
            Pose = MovementStep.ApplyTick(in Pose, in cmd, in _context);
        }
    }

    private static int CompareTick(InputCmd left, InputCmd right) => left.Tick.CompareTo(right.Tick);
}
