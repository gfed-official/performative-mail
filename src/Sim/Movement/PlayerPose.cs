using System;

namespace PerformativeMail.Sim.Movement;

public readonly record struct PlayerPose(int Xcm, int Ycm, int Zcm, ushort Yaw)
{
    public static PlayerPose Origin { get; } = new(0, 0, 0, 0);

    public static int QuantizeCm(double meters) =>
        (int)Math.Round(meters * 100.0, MidpointRounding.AwayFromZero);

    public static PlayerPose FromMeters(double x, double y, double z, ushort yaw) =>
        new(QuantizeCm(x), QuantizeCm(y), QuantizeCm(z), yaw);
}
