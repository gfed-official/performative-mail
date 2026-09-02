using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Movement;

public static class MovementStep
{
    public const float DisplacementClampFactor = 1.15f;
    public const sbyte AxisFull = 127;

    public static PlayerPose ApplyTick(in PlayerPose pose, in InputCmd cmd, in MovementContext context) =>
        Apply(in pose, in cmd, in context, TickClock.TickDurationSeconds);

    public static PlayerPose Apply(in PlayerPose pose, in InputCmd cmd, in MovementContext context, double dtSeconds)
    {
        if (dtSeconds <= 0)
            return new PlayerPose(pose.Xcm, pose.Ycm, pose.Zcm, cmd.Yaw);

        var maxSpeed = context.MaxSpeedMetersPerSecond(cmd.Buttons);
        var axisX = cmd.AxisX / (double)AxisFull;
        var axisY = cmd.AxisY / (double)AxisFull;

        // Yaw 0 faces +Y (north). Units increase clockwise toward +X (east). ch08 §4.
        var yawRad = cmd.Yaw * (Math.PI * 2.0 / 65536.0);
        var sin = Math.Sin(yawRad);
        var cos = Math.Cos(yawRad);
        var east = axisX * cos + axisY * sin;
        var north = -axisX * sin + axisY * cos;

        var dx = east * maxSpeed * dtSeconds;
        var dy = north * maxSpeed * dtSeconds;
        var mag = Math.Sqrt(dx * dx + dy * dy);
        var maxDisp = maxSpeed * DisplacementClampFactor * dtSeconds;
        if (mag > maxDisp && mag > 0)
        {
            var scale = maxDisp / mag;
            dx *= scale;
            dy *= scale;
        }

        return PlayerPose.FromMeters(
            pose.Xcm / 100.0 + dx,
            pose.Ycm / 100.0 + dy,
            pose.Zcm / 100.0,
            cmd.Yaw);
    }
}
