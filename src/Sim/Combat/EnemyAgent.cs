using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Vehicles;

namespace PerformativeMail.Sim.Combat;

public enum EnemyPhase : byte
{
    Spawned = 1,
    Marching = 2,
    Arrived = 3,
}

public sealed class EnemyAgent
{
    private readonly PathCursor _cursor;

    internal EnemyAgent(EntityId id, EnemyKind kind, EnemyStats stats, RoutePath path, PathCursor cursor)
    {
        Id = id;
        Kind = kind;
        Hp = stats.Hp;
        SpeedMetresPerSecond = stats.SpeedMetresPerSecond;
        Path = path;
        _cursor = cursor;
        Phase = EnemyPhase.Spawned;
        Pose = cursor.Pose;
    }

    public EntityId Id { get; }

    public EnemyKind Kind { get; }

    public EnemyPhase Phase { get; private set; }

    public int Hp { get; }

    public double SpeedMetresPerSecond { get; }

    public RoutePath Path { get; }

    public PlayerPose Pose { get; private set; }

    public double MetresAlong => _cursor.MetresAlong;

    internal void Step(float dt)
    {
        switch (Phase)
        {
            case EnemyPhase.Spawned:
            case EnemyPhase.Marching:
                _cursor.Advance(SpeedMetresPerSecond * dt);
                Pose = _cursor.Pose;
                Phase = _cursor.AtEnd ? EnemyPhase.Arrived : EnemyPhase.Marching;
                return;
            case EnemyPhase.Arrived:
                return;
            default:
            {
                EnemyPhase unseen = Phase;
                throw new ArgumentOutOfRangeException(nameof(Phase), unseen, null);
            }
        }
    }
}
