using System;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.App;

public readonly record struct ViewPose(float X, float Y, float Z, float YawRadians);

public static class ViewFrame
{
    public static ViewPose From(in PlayerPose pose)
    {
        float yaw = (float)(-pose.Yaw * (Math.PI * 2.0 / 65536.0));
        return new ViewPose(
            pose.Xcm / 100f,
            pose.Zcm / 100f,
            -pose.Ycm / 100f,
            yaw);
    }
}
