using System;

namespace PerformativeMail.Client.UI;

public sealed class PauseMenuState
{
    public bool IsOpen { get; private set; }

    public bool WantsLeave { get; private set; }

    public PauseSnapshot Snapshot { get; private set; } = PauseBoot.Root(false);

    public PauseFrame Frame => PauseFrame.From(Snapshot);

    public void Open(bool clockPaused)
    {
        IsOpen = true;
        WantsLeave = false;
        Snapshot = Snapshot with { Screen = PauseScreen.Root, ClockPaused = clockPaused };
    }

    public void Close()
    {
        WantsLeave = false;
        Hide();
    }

    public void Toggle(bool clockPaused)
    {
        if (IsOpen)
            Close();
        else
            Open(clockPaused);
    }

    public void SetClockPaused(bool clockPaused) =>
        Snapshot = Snapshot with { ClockPaused = clockPaused };

    public bool Back()
    {
        if (!IsOpen)
            return false;

        if (Snapshot.Screen == PauseScreen.Root)
        {
            Close();
            return false;
        }

        Snapshot = Snapshot with { Screen = PauseScreen.Root };
        return true;
    }

    public void Apply(string choiceId)
    {
        switch (choiceId)
        {
            case PauseFrame.ResumeId:
                Close();
                break;
            case PauseFrame.ControlsId:
                Snapshot = Snapshot with { Screen = PauseScreen.Controls };
                break;
            case PauseFrame.OptionsId:
                Snapshot = Snapshot with { Screen = PauseScreen.Options };
                break;
            case PauseFrame.LeaveId:
                Snapshot = Snapshot with { Screen = PauseScreen.LeaveConfirm };
                break;
            case PauseFrame.ConfirmLeaveId:
                WantsLeave = true;
                Hide();
                break;
            case PauseFrame.CancelId:
            case PauseFrame.BackId:
                Snapshot = Snapshot with { Screen = PauseScreen.Root };
                break;
            case PauseFrame.VolumeDownId:
                Snapshot = Snapshot with
                {
                    VolumePct = PauseBoot.ClampVolume(Snapshot.VolumePct - PauseBoot.VolumeStep),
                };
                break;
            case PauseFrame.VolumeUpId:
                Snapshot = Snapshot with
                {
                    VolumePct = PauseBoot.ClampVolume(Snapshot.VolumePct + PauseBoot.VolumeStep),
                };
                break;
            case PauseFrame.TextScaleDownId:
                Snapshot = Snapshot with
                {
                    TextScalePct = PauseBoot.ClampTextScale(Snapshot.TextScalePct - PauseBoot.TextScaleStep),
                };
                break;
            case PauseFrame.TextScaleUpId:
                Snapshot = Snapshot with
                {
                    TextScalePct = PauseBoot.ClampTextScale(Snapshot.TextScalePct + PauseBoot.TextScaleStep),
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(choiceId), choiceId, null);
        }
    }

    private void Hide()
    {
        IsOpen = false;
        Snapshot = Snapshot with { Screen = PauseScreen.Root };
    }
}
