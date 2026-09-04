using PerformativeMail.Client.UI;

namespace PerformativeMail.Net.Tests.UI;

public sealed class PauseMenuStateTests
{
    [Fact]
    public void Toggle_OpensRootAndCloses()
    {
        var menu = new PauseMenuState();
        Assert.False(menu.IsOpen);

        menu.Toggle(clockPaused: true);
        Assert.True(menu.IsOpen);
        Assert.Equal(PauseScreen.Root, menu.Snapshot.Screen);
        Assert.Equal(PauseFrame.SoloStatus, menu.Frame.StatusLabel);

        menu.Toggle(clockPaused: true);
        Assert.False(menu.IsOpen);
        Assert.False(menu.WantsLeave);
    }

    [Fact]
    public void Back_FromRoot_Closes_FromSubmenu_ReturnsRoot()
    {
        var menu = new PauseMenuState();
        menu.Open(clockPaused: false);
        menu.Apply(PauseFrame.ControlsId);
        Assert.Equal(PauseScreen.Controls, menu.Snapshot.Screen);
        Assert.True(menu.Back());
        Assert.True(menu.IsOpen);
        Assert.Equal(PauseScreen.Root, menu.Snapshot.Screen);

        Assert.False(menu.Back());
        Assert.False(menu.IsOpen);
    }

    [Fact]
    public void Apply_Resume_ClosesWithoutLeave()
    {
        var menu = new PauseMenuState();
        menu.Open(false);
        menu.Apply(PauseFrame.ResumeId);
        Assert.False(menu.IsOpen);
        Assert.False(menu.WantsLeave);
    }

    [Fact]
    public void Apply_Leave_RequiresConfirm()
    {
        var menu = new PauseMenuState();
        menu.Open(false);
        menu.Apply(PauseFrame.LeaveId);
        Assert.True(menu.IsOpen);
        Assert.Equal(PauseScreen.LeaveConfirm, menu.Snapshot.Screen);
        Assert.False(menu.WantsLeave);
        Assert.Equal(PauseFrame.LeaveBody, menu.Frame.Body);

        menu.Apply(PauseFrame.CancelId);
        Assert.True(menu.IsOpen);
        Assert.Equal(PauseScreen.Root, menu.Snapshot.Screen);

        menu.Apply(PauseFrame.LeaveId);
        menu.Apply(PauseFrame.ConfirmLeaveId);
        Assert.False(menu.IsOpen);
        Assert.True(menu.WantsLeave);
    }

    [Fact]
    public void Apply_Options_NudgesAndClamps()
    {
        var menu = new PauseMenuState();
        menu.Open(false);
        menu.Apply(PauseFrame.OptionsId);
        Assert.Equal(PauseScreen.Options, menu.Snapshot.Screen);
        Assert.Equal(PauseBoot.DefaultVolumePct, menu.Snapshot.VolumePct);
        Assert.Equal(PauseBoot.DefaultTextScalePct, menu.Snapshot.TextScalePct);

        menu.Apply(PauseFrame.VolumeDownId);
        Assert.Equal(90, menu.Snapshot.VolumePct);
        for (int i = 0; i < 20; i++)
            menu.Apply(PauseFrame.VolumeDownId);
        Assert.Equal(PauseBoot.MinVolumePct, menu.Snapshot.VolumePct);

        menu.Apply(PauseFrame.TextScaleUpId);
        Assert.Equal(110, menu.Snapshot.TextScalePct);
        for (int i = 0; i < 20; i++)
            menu.Apply(PauseFrame.TextScaleUpId);
        Assert.Equal(PauseBoot.MaxTextScalePct, menu.Snapshot.TextScalePct);

        menu.Apply(PauseFrame.TextScaleDownId);
        Assert.Equal(140, menu.Snapshot.TextScalePct);
        menu.Apply(PauseFrame.VolumeUpId);
        Assert.Equal(10, menu.Snapshot.VolumePct);
    }

    [Fact]
    public void SetClockPaused_UpdatesRootStatus()
    {
        var menu = new PauseMenuState();
        menu.Open(clockPaused: true);
        Assert.Equal(PauseFrame.SoloStatus, menu.Frame.StatusLabel);
        menu.SetClockPaused(false);
        Assert.Equal(PauseFrame.MultiStatus, menu.Frame.StatusLabel);
    }
}
