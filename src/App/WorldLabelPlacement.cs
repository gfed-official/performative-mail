using System;

namespace PerformativeMail.App;

public static class WorldLabelPlacement
{
    public const float RoofClearanceMeters = 0.45f;
    public const float FaceClearanceMeters = 0.2f;

    public static (float X, float Y, float Z) AboveStreetFace(
        float sizeX,
        float sizeY,
        float sizeZ,
        float heightCenter,
        float towardX,
        float towardZ)
    {
        float y = heightCenter + sizeY * 0.5f + RoofClearanceMeters;
        if (towardX == 0f && towardZ == 0f)
            return (0f, y, 0f);

        if (MathF.Abs(towardX) >= MathF.Abs(towardZ))
            return (MathF.Sign(towardX) * (sizeX * 0.5f + FaceClearanceMeters), y, 0f);

        return (0f, y, MathF.Sign(towardZ) * (sizeZ * 0.5f + FaceClearanceMeters));
    }
}
