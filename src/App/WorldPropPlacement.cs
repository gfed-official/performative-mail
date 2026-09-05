using System;

namespace PerformativeMail.App;

public static class WorldPropPlacement
{
    public const float HouseRoofScale = 0.9f;
    public const float HouseRoofHeightMeters = 0.55f;
    public const float MailboxFlagThicknessMeters = 0.04f;
    public const float MailboxFlagHeightMeters = 0.18f;
    public const float MailboxFlagLengthMeters = 0.28f;
    public const float MailboxFlagGapMeters = 0.03f;
    public const float MailboxFlagCenterY = 1.0f;

    public static (float X, float Y, float Z) RoofSize(float bodyX, float bodyZ) =>
        (bodyX * HouseRoofScale, HouseRoofHeightMeters, bodyZ * HouseRoofScale);

    public static float RoofCenterY(float bodyHeight) =>
        bodyHeight + HouseRoofHeightMeters * 0.5f;

    public static float MailboxFlagOutboard(float bodyExtent) =>
        bodyExtent * 0.5f + MailboxFlagGapMeters + MailboxFlagLengthMeters * 0.5f;

    public static (float X, float Y, float Z) MailboxFlagOffset(
        float bodyX,
        float bodyZ,
        float towardX,
        float towardZ)
    {
        if (towardX == 0f && towardZ == 0f)
            return (MailboxFlagOutboard(bodyX), MailboxFlagCenterY, 0f);

        if (MathF.Abs(towardX) >= MathF.Abs(towardZ))
            return (MathF.Sign(towardX) * MailboxFlagOutboard(bodyX), MailboxFlagCenterY, 0f);

        return (0f, MailboxFlagCenterY, MathF.Sign(towardZ) * MailboxFlagOutboard(bodyZ));
    }

    public static (float X, float Y, float Z) MailboxFlagSize(float towardX, float towardZ)
    {
        if (MathF.Abs(towardZ) > MathF.Abs(towardX))
            return (MailboxFlagThicknessMeters, MailboxFlagHeightMeters, MailboxFlagLengthMeters);

        return (MailboxFlagLengthMeters, MailboxFlagHeightMeters, MailboxFlagThicknessMeters);
    }
}
