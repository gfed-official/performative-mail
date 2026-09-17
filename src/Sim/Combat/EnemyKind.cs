using System;

namespace PerformativeMail.Sim.Combat;

public enum EnemyKind : byte
{
    Barbarian = 1,
}

internal readonly record struct EnemyStats(int Hp, double SpeedMetresPerSecond)
{
    public static EnemyStats ForKind(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Barbarian:
                return new EnemyStats(60, 4.5);
            default:
            {
                EnemyKind unseen = kind;
                throw new ArgumentOutOfRangeException(nameof(kind), unseen, null);
            }
        }
    }
}
