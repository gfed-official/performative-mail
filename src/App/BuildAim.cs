using System;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class BuildAim
{
    public static bool TryTile(
        in PlayerPose pose,
        float pitchRadians,
        int tileCm,
        int width,
        int height,
        out TileCoord tile)
    {
        tile = default;
        if (tileCm <= 0 || width <= 0 || height <= 0)
            return false;

        var view = ViewFrame.From(in pose);
        var eye = FirstPersonLook.EyePose(in view);
        float yaw = view.YawRadians;
        float cp = MathF.Cos(pitchRadians);
        float dirX = -cp * MathF.Sin(yaw);
        float dirY = MathF.Sin(pitchRadians);
        float dirZ = -cp * MathF.Cos(yaw);

        float hitX;
        float hitZ;
        if (dirY < -1e-4f)
        {
            float t = -eye.Y / dirY;
            hitX = eye.X + t * dirX;
            hitZ = eye.Z + t * dirZ;
        }
        else
        {
            hitX = eye.X;
            hitZ = eye.Z;
        }

        int x = (int)Math.Floor(hitX * 100.0 / tileCm);
        int y = (int)Math.Floor(-hitZ * 100.0 / tileCm);
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            return false;

        tile = new TileCoord(x, y);
        return true;
    }
}
