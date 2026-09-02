using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Movement;

public readonly record struct MovementContext(int WeightPoints)
{
    public const float WalkMetersPerSecond = 5.0f;
    public const float SprintMetersPerSecond = 7.5f;
    public const float WeightSpeedFloor = 0.6f;

    public static MovementContext Unburdened { get; } = new(0);

    public float WeightMultiplier =>
        Math.Clamp(1.0f - 0.01f * WeightPoints, WeightSpeedFloor, 1.0f);

    public float MaxSpeedMetersPerSecond(InputButtons buttons)
    {
        var top = (buttons & InputButtons.Sprint) != 0
            ? SprintMetersPerSecond
            : WalkMetersPerSecond;
        return top * WeightMultiplier;
    }
}
