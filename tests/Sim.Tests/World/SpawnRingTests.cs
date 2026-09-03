using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class SpawnRingTests
{
    [Fact]
    public void CentreOf_NullAtlas_IsOrigin()
    {
        Assert.Equal(PlayerPose.Origin, SpawnRing.CentreOf(null));
    }

    [Fact]
    public void Pose_OrdinalZero_IsCentre()
    {
        var centre = new PlayerPose(500, 500, 0, 0);
        Assert.Equal(centre, SpawnRing.Pose(in centre, 0));
    }

    [Fact]
    public void Pose_OrdinalOne_IsRadiusAway()
    {
        var centre = PlayerPose.Origin;
        var pose = SpawnRing.Pose(in centre, 1);
        Assert.Equal(SpawnRing.RadiusCm, DistanceCm(centre, pose));
        Assert.NotEqual(centre, pose);
    }

    private static int DistanceCm(PlayerPose a, PlayerPose b)
    {
        int dx = a.Xcm - b.Xcm;
        int dy = a.Ycm - b.Ycm;
        return (int)System.Math.Round(System.Math.Sqrt(dx * dx + dy * dy), System.MidpointRounding.AwayFromZero);
    }
}
