using PerformativeMail.Client.UI;

namespace PerformativeMail.Net.Tests.UI;

public sealed class PauseFrameTests
{
    [Fact]
    public void From_RootSolo_PausedStatusAndChoices()
    {
        var frame = PauseFrame.From(PauseBoot.Root(clockPaused: true));

        Assert.Equal(PauseFrame.RootTitle, frame.Title);
        Assert.Equal(PauseFrame.SoloStatus, frame.StatusLabel);
        Assert.Equal("", frame.Body);
        Assert.Empty(frame.Binds);
        Assert.Empty(frame.Options);
        Assert.Equal(
            new[]
            {
                PauseFrame.ResumeText,
                PauseFrame.ControlsText,
                PauseFrame.OptionsText,
                PauseFrame.LeaveText,
            },
            Ids(frame.Choices, c => c.Label));
        Assert.Equal(
            new[]
            {
                PauseFrame.ResumeId,
                PauseFrame.ControlsId,
                PauseFrame.OptionsId,
                PauseFrame.LeaveId,
            },
            Ids(frame.Choices, c => c.Id));
    }

    [Fact]
    public void From_RootMulti_RunContinues()
    {
        var frame = PauseFrame.From(PauseBoot.Root(clockPaused: false));
        Assert.Equal(PauseFrame.MultiStatus, frame.StatusLabel);
    }

    [Fact]
    public void From_Controls_ListsDefaultBinds()
    {
        var frame = PauseFrame.From(PauseBoot.Root(false) with { Screen = PauseScreen.Controls });

        Assert.Equal(PauseFrame.ControlsTitle, frame.Title);
        Assert.Equal(PauseBoot.Binds, frame.Binds);
        Assert.Contains(frame.Binds, bind => bind.Action == "Pause menu" && bind.Keyboard == "Esc");
        Assert.Contains(frame.Binds, bind => bind.Action == "Move / look");
        Assert.Equal(PauseFrame.BackId, Assert.Single(frame.Choices).Id);
    }

    [Fact]
    public void From_Options_VolumeAndTextScaleStubs()
    {
        var snap = PauseBoot.Root(false) with { Screen = PauseScreen.Options, VolumePct = 80, TextScalePct = 120 };
        var frame = PauseFrame.From(in snap);

        Assert.Equal(PauseFrame.OptionsTitle, frame.Title);
        Assert.Equal(PauseFrame.OptionsBody, frame.Body);
        Assert.Equal(2, frame.Options.Count);
        Assert.Equal(PauseBoot.VolumeId, frame.Options[0].Id);
        Assert.Equal(PauseBoot.VolumeLabel, frame.Options[0].Label);
        Assert.Equal("80%", frame.Options[0].Value);
        Assert.Equal(PauseBoot.TextScaleId, frame.Options[1].Id);
        Assert.Equal("120%", frame.Options[1].Value);
        Assert.Equal(PauseFrame.BackId, Assert.Single(frame.Choices).Id);
    }

    [Fact]
    public void From_LeaveConfirm_ExplainsDropAfter120s()
    {
        var frame = PauseFrame.From(PauseBoot.Root(false) with { Screen = PauseScreen.LeaveConfirm });

        Assert.Equal(PauseFrame.LeaveTitle, frame.Title);
        Assert.Equal(PauseFrame.LeaveBody, frame.Body);
        Assert.Contains("120 s", frame.Body);
        Assert.Equal(PauseFrame.ConfirmLeaveId, frame.Choices[0].Id);
        Assert.Equal(PauseFrame.CancelId, frame.Choices[1].Id);
    }

    [Fact]
    public void OptionIds_UseDownUpSuffix()
    {
        Assert.Equal(PauseFrame.VolumeDownId, PauseFrame.OptionDownId(PauseBoot.VolumeId));
        Assert.Equal(PauseFrame.VolumeUpId, PauseFrame.OptionUpId(PauseBoot.VolumeId));
        Assert.Equal(PauseFrame.TextScaleDownId, PauseFrame.OptionDownId(PauseBoot.TextScaleId));
        Assert.Equal(PauseFrame.TextScaleUpId, PauseFrame.OptionUpId(PauseBoot.TextScaleId));
    }

    private static string[] Ids<T>(IReadOnlyList<T> items, Func<T, string> pick)
    {
        var ids = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
            ids[i] = pick(items[i]);
        return ids;
    }
}
