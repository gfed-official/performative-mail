using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Vehicles;

public static class VehicleStep
{
    public static PlayerPose ApplyTick(in PlayerPose pose, in InputCmd cmd, in VehicleContext context) =>
        Apply(in pose, in cmd, in context, TickClock.TickDurationSeconds);

    public static PlayerPose Apply(in PlayerPose pose, in InputCmd cmd, in VehicleContext context, double dtSeconds)
    {
        if (dtSeconds <= 0)
            return new PlayerPose(pose.Xcm, pose.Ycm, pose.Zcm, cmd.Yaw);

        var maxSpeed = context.MaxSpeedMetersPerSecond;
        var axisX = cmd.AxisX / (double)MovementStep.AxisFull;
        var axisY = cmd.AxisY / (double)MovementStep.AxisFull;

        var yawRad = cmd.Yaw * (Math.PI * 2.0 / 65536.0);
        var sin = Math.Sin(yawRad);
        var cos = Math.Cos(yawRad);
        var east = axisX * cos + axisY * sin;
        var north = -axisX * sin + axisY * cos;

        var dx = east * maxSpeed * dtSeconds;
        var dy = north * maxSpeed * dtSeconds;
        var mag = Math.Sqrt(dx * dx + dy * dy);
        var maxDisp = maxSpeed * VehicleContext.SpeedClampFactor * dtSeconds;
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
