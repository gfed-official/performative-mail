using System;
using System.Collections.Generic;

namespace PerformativeMail.Client.UI;

public readonly record struct PauseBind(string Action, string Keyboard, string Gamepad);

public readonly record struct PauseOption(string Id, string Label, string Value);

public readonly record struct PauseChoice(string Id, string Label);

public readonly record struct PauseSnapshot(
    PauseScreen Screen,
    bool ClockPaused,
    int VolumePct,
    int TextScalePct);

public readonly record struct PauseFrame(
    string Title,
    string StatusLabel,
    string Body,
    IReadOnlyList<PauseBind> Binds,
    IReadOnlyList<PauseOption> Options,
    IReadOnlyList<PauseChoice> Choices)
{
    public const string ResumeId = "resume";
    public const string ControlsId = "controls";
    public const string OptionsId = "options";
    public const string LeaveId = "leave";
    public const string ConfirmLeaveId = "confirm-leave";
    public const string CancelId = "cancel";
    public const string BackId = "back";
    public const string VolumeDownId = "volume-down";
    public const string VolumeUpId = "volume-up";
    public const string TextScaleDownId = "text-scale-down";
    public const string TextScaleUpId = "text-scale-up";

    public const string ResumeText = "Resume";
    public const string ControlsText = "Controls";
    public const string OptionsText = "Options";
    public const string LeaveText = "Leave run";
    public const string ConfirmLeaveText = "Leave";
    public const string CancelText = "Cancel";
    public const string BackText = "Back";

    public const string RootTitle = "Paused";
    public const string ControlsTitle = "Controls";
    public const string OptionsTitle = "Options";
    public const string LeaveTitle = "Leave run";

    public const string SoloStatus = "Server paused";
    public const string MultiStatus = "Run continues";
    public const string LeaveBody = "Inventory is dropped after 120 s.";
    public const string OptionsBody = "Local only. Not saved.";

    public static string OptionDownId(string optionId) => optionId + "-down";

    public static string OptionUpId(string optionId) => optionId + "-up";

    public static PauseFrame From(in PauseSnapshot snapshot)
    {
        switch (snapshot.Screen)
        {
            case PauseScreen.Root:
                return new PauseFrame(
                    RootTitle,
                    snapshot.ClockPaused ? SoloStatus : MultiStatus,
                    "",
                    Array.Empty<PauseBind>(),
                    Array.Empty<PauseOption>(),
                    new[]
                    {
                        new PauseChoice(ResumeId, ResumeText),
                        new PauseChoice(ControlsId, ControlsText),
                        new PauseChoice(OptionsId, OptionsText),
                        new PauseChoice(LeaveId, LeaveText),
                    });
            case PauseScreen.Controls:
                return new PauseFrame(
                    ControlsTitle,
                    "",
                    "",
                    PauseBoot.Binds,
                    Array.Empty<PauseOption>(),
                    new[] { new PauseChoice(BackId, BackText) });
            case PauseScreen.Options:
                return new PauseFrame(
                    OptionsTitle,
                    "",
                    OptionsBody,
                    Array.Empty<PauseBind>(),
                    new[]
                    {
                        new PauseOption(
                            PauseBoot.VolumeId,
                            PauseBoot.VolumeLabel,
                            snapshot.VolumePct + "%"),
                        new PauseOption(
                            PauseBoot.TextScaleId,
                            PauseBoot.TextScaleLabel,
                            snapshot.TextScalePct + "%"),
                    },
                    new[] { new PauseChoice(BackId, BackText) });
            case PauseScreen.LeaveConfirm:
                return new PauseFrame(
                    LeaveTitle,
                    "",
                    LeaveBody,
                    Array.Empty<PauseBind>(),
                    Array.Empty<PauseOption>(),
                    new[]
                    {
                        new PauseChoice(ConfirmLeaveId, ConfirmLeaveText),
                        new PauseChoice(CancelId, CancelText),
                    });
            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot.Screen), snapshot.Screen, null);
        }
    }
}
