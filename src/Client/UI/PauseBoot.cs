using System;
using System.Collections.Generic;

namespace PerformativeMail.Client.UI;

public static class PauseBoot
{
    public const string VolumeId = "volume";
    public const string TextScaleId = "text-scale";
    public const string VolumeLabel = "Volume";
    public const string TextScaleLabel = "Text scale";

    public const int DefaultVolumePct = 100;
    public const int DefaultTextScalePct = 100;
    public const int VolumeStep = 10;
    public const int TextScaleStep = 10;
    public const int MinVolumePct = 0;
    public const int MaxVolumePct = 100;
    public const int MinTextScalePct = 100;
    public const int MaxTextScalePct = 150;

    public static IReadOnlyList<PauseBind> Binds { get; } = new PauseBind[]
    {
        new("Move / look", "WASD / mouse", "Left stick / right stick"),
        new("Sprint", "Shift", "L3"),
        new("Jump", "Space", "A"),
        new("Interact / deliver", "E (hold)", "X (hold)"),
        new("Deliver all matching", "F", "Hold X longer (1 s)"),
        new("Attack / use tool", "LMB", "RT"),
        new("Inventory", "Tab", "Y"),
        new("Map", "M", "View"),
        new("Build mode", "B", "LB"),
        new("Rotate (build / inventory)", "R", "RB"),
        new("Hotbar select", "1-8 / scroll", "D-pad L/R"),
        new("Ping", "Middle mouse (hold for wheel)", "RS click"),
        new("Chat", "Enter", "Menu (radial emotes)"),
        new("Pause menu", "Esc", "Menu"),
    };

    public static PauseSnapshot Root(bool clockPaused) =>
        new(PauseScreen.Root, clockPaused, DefaultVolumePct, DefaultTextScalePct);

    public static int ClampVolume(int pct) => Math.Clamp(pct, MinVolumePct, MaxVolumePct);

    public static int ClampTextScale(int pct) => Math.Clamp(pct, MinTextScalePct, MaxTextScalePct);
}
