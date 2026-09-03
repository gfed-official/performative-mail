using System;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.World;

public static class SpawnRing
{
    public const int RadiusCm = 150;

    public const int Slots = 6;

    public static PlayerPose CentreOf(WorldAtlas? atlas)
    {
        if (atlas is null)
            return PlayerPose.Origin;

        var pad = atlas.PostOffice.SpawnPadTile;
        int half = atlas.TileCm / 2;
        return new PlayerPose(
            pad.X * atlas.TileCm + half,
            pad.Y * atlas.TileCm + half,
            0,
            0);
    }

    public static PlayerPose Pose(in PlayerPose centre, uint ordinal)
    {
        if (ordinal == 0)
            return centre;

        uint index = ordinal - 1;
        int slot = (int)(index % Slots);
        int ring = (int)(index / Slots);
        double radius = RadiusCm * (ring + 1);
        double angle = slot / (double)Slots * (Math.PI * 2.0);
        int xcm = centre.Xcm + (int)Math.Round(Math.Cos(angle) * radius, MidpointRounding.AwayFromZero);
        int ycm = centre.Ycm + (int)Math.Round(Math.Sin(angle) * radius, MidpointRounding.AwayFromZero);
        int dx = centre.Xcm - xcm;
        int dy = centre.Ycm - ycm;
        double yawRad = Math.Atan2(dx, dy);
        var yaw = (ushort)((int)Math.Round(yawRad * 65536.0 / (Math.PI * 2.0), MidpointRounding.AwayFromZero) & 0xFFFF);
        return new PlayerPose(xcm, ycm, centre.Zcm, yaw);
    }
}
