namespace PerformativeMail.App;

public static class PawnLabelPlacement
{
    public const float HeightMeters = 2.0f;

    public static (float X, float Y, float Z) AbovePawn() => (0f, HeightMeters, 0f);
}
