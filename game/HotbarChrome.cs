using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public static class HotbarChrome
{
    public static Color SlotIdle => PlayTheme.Border;
    public static Color SlotSelected => PlayTheme.Primary;
    public static readonly Color LetterFill = new(0.97f, 0.95f, 0.87f);
    public static readonly Color PackageFill = new(0.75f, 0.48f, 0.24f);
    public static readonly Color ItemFill = new(0.24f, 0.49f, 1f);
    public static readonly Color HandsFill = new(0.60f, 0.64f, 0.70f);

    public static Color Fill(OverlayIcon icon) => icon switch
    {
        OverlayIcon.Empty => new Color(0f, 0f, 0f, 0f),
        OverlayIcon.Hands => HandsFill,
        OverlayIcon.Letter => LetterFill,
        OverlayIcon.Package => PackageFill,
        OverlayIcon.Item => ItemFill,
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
    };

    public static Vector2 Size(OverlayIcon icon) => icon switch
    {
        OverlayIcon.Empty => Vector2.Zero,
        OverlayIcon.Hands => new Vector2(16, 16),
        OverlayIcon.Letter => new Vector2(28, 16),
        OverlayIcon.Package => new Vector2(20, 22),
        OverlayIcon.Item => new Vector2(16, 16),
        _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, null),
    };
}
