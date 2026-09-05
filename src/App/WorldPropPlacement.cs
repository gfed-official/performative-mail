using System;

namespace PerformativeMail.App;

public static class WorldPropPlacement
{
    public const float HouseRoofScale = 0.9f;
    public const float HouseRoofHeightMeters = 0.55f;
    public const float MailboxFlagThicknessMeters = 0.02f;
    public const float MailboxFlagHeightMeters = 0.12f;
    public const float MailboxFlagLengthMeters = 0.22f;
    public const float MailboxFlagCenterY = 0.95f;

    public static (float X, float Y, float Z) RoofSize(float bodyX, float bodyZ) =>
        (bodyX * HouseRoofScale, HouseRoofHeightMeters, bodyZ * HouseRoofScale);

    public static float RoofCenterY(float bodyHeight) =>
        bodyHeight + HouseRoofHeightMeters * 0.5f;

    public static (float X, float Y, float Z) MailboxFlagOffset(
        float bodyX,
        float bodyZ,
        float towardX,
        float towardZ)
    {
        float alongX = bodyX * 0.5f + MailboxFlagThicknessMeters * 0.5f;
        float alongZ = bodyZ * 0.5f + MailboxFlagThicknessMeters * 0.5f;
        if (towardX == 0f && towardZ == 0f)
            return (alongX, MailboxFlagCenterY, 0f);

        if (MathF.Abs(towardX) >= MathF.Abs(towardZ))
            return (MathF.Sign(towardX) * alongX, MailboxFlagCenterY, 0f);

        return (0f, MailboxFlagCenterY, MathF.Sign(towardZ) * alongZ);
    }

    public static (float X, float Y, float Z) MailboxFlagSize(float towardX, float towardZ)
    {
        if (MathF.Abs(towardZ) > MathF.Abs(towardX))
            return (MailboxFlagLengthMeters, MailboxFlagHeightMeters, MailboxFlagThicknessMeters);

        return (MailboxFlagThicknessMeters, MailboxFlagHeightMeters, MailboxFlagLengthMeters);
    }
}
