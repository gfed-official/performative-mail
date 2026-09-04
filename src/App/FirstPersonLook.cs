using System;

namespace PerformativeMail.App;

public struct FirstPersonLookState
{
    public ushort Yaw;
    public float PitchRadians;
}

public static class FirstPersonLook
{
    public const float EyeHeightMeters = 1.6f;
    public const float RadiansPerPixel = 0.0025f;
    public const float MaxPitchRadians = 1.2f;
    public const float YawUnitsPerTurn = 65536f;

    public static void ApplyMouse(ref FirstPersonLookState state, float deltaX, float deltaY)
    {
        float yawDelta = deltaX * RadiansPerPixel;
        int yawUnits = (int)Math.Round(yawDelta * YawUnitsPerTurn / (Math.PI * 2.0));
        state.Yaw = (ushort)(state.Yaw + yawUnits);

        state.PitchRadians = Math.Clamp(
            state.PitchRadians - deltaY * RadiansPerPixel,
            -MaxPitchRadians,
            MaxPitchRadians);
    }

    public static ViewPose EyePose(in ViewPose feet) =>
        new(feet.X, feet.Y + EyeHeightMeters, feet.Z, feet.YawRadians);
}
