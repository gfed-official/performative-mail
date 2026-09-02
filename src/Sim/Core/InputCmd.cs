using System;

namespace PerformativeMail.Sim.Core;

[Flags]
public enum InputButtons : ushort
{
    None = 0,
    Sprint = 1,
    Jump = 2,
    Interact = 4,
    Attack = 8,
}

public readonly record struct InputCmd(
    uint Tick,
    sbyte AxisX,
    sbyte AxisY,
    ushort Yaw,
    InputButtons Buttons);
