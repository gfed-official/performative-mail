using PerformativeMail.App;

namespace PerformativeMail.Net.Tests.App;

public sealed class FirstPersonLookTests
{
    [Fact]
    public void EyePose_RaisesFeetByEyeHeight()
    {
        var feet = new ViewPose(1f, 2f, 3f, 0.5f);
        var eye = FirstPersonLook.EyePose(in feet);
        Assert.Equal(1f, eye.X);
        Assert.Equal(2f + FirstPersonLook.EyeHeightMeters, eye.Y);
        Assert.Equal(3f, eye.Z);
        Assert.Equal(0.5f, eye.YawRadians);
    }

    [Fact]
    public void ApplyMouse_UpdatesYawAndClampsPitch()
    {
        var look = new FirstPersonLookState();
        FirstPersonLook.ApplyMouse(ref look, 100f, -10_000f);
        Assert.NotEqual((ushort)0, look.Yaw);
        Assert.Equal(FirstPersonLook.MaxPitchRadians, look.PitchRadians, 3);
        FirstPersonLook.ApplyMouse(ref look, 0f, 20_000f);
        Assert.Equal(-FirstPersonLook.MaxPitchRadians, look.PitchRadians, 3);
    }
}
