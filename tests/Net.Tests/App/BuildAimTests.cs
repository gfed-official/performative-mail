using PerformativeMail.App;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class BuildAimTests
{
    [Fact]
    public void LookDownFromOrigin_HitsTileZero()
    {
        var pose = new PlayerPose(100, 100, 0, 0);
        Assert.True(BuildAim.TryTile(in pose, -1.2f, 200, 16, 12, out var tile));
        Assert.Equal(0, tile.X);
        Assert.Equal(0, tile.Y);
    }

    [Fact]
    public void OutOfBounds_Rejected()
    {
        var pose = new PlayerPose(-400, -400, 0, 0);
        Assert.False(BuildAim.TryTile(in pose, -0.9f, 200, 16, 12, out _));
    }
}
