using Godot;

namespace PerformativeMail.Game;

public static class PlayTheme
{
    public const string PanelBgHex = "#1A2433";
    public const string BorderHex = "#3D5A80";
    public const string PrimaryHex = "#3D7EFF";
    public const string PrimaryHoverHex = "#5A93FF";
    public const string DangerHex = "#E85D3A";
    public const string BodyHex = "#ECF0F1";
    public const string MutedHex = "#9AA4B2";

    public const float PanelOpacity = 0.92f;
    public const int CornerRadius = 8;
    public const int BorderWidth = 2;

    public static readonly Color PanelBg = Rgb(0x1A2433, PanelOpacity);
    public static readonly Color Border = Rgb(0x3D5A80);
    public static readonly Color Primary = Rgb(0x3D7EFF);
    public static readonly Color PrimaryHover = Rgb(0x5A93FF);
    public static readonly Color Danger = Rgb(0xE85D3A);
    public static readonly Color Body = Rgb(0xECF0F1);
    public static readonly Color Muted = Rgb(0x9AA4B2);

    public const string MutedType = "Muted";

    private static Theme? _shared;

    public static Theme Shared => _shared ??= Build();

    public static Theme Build()
    {
        var theme = new Theme();
        theme.SetStylebox("panel", "PanelContainer", PanelBox(PanelBg));

        var normal = ButtonBox(Primary);
        var hover = ButtonBox(PrimaryHover);
        theme.SetStylebox("normal", "Button", normal);
        theme.SetStylebox("hover", "Button", hover);
        theme.SetStylebox("pressed", "Button", hover);
        theme.SetStylebox("focus", "Button", hover);
        theme.SetStylebox("disabled", "Button", ButtonBox(new Color(Primary.R, Primary.G, Primary.B, 0.4f)));

        theme.SetColor("font_color", "Button", Colors.White);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", Colors.White);
        theme.SetColor("font_focus_color", "Button", Colors.White);
        theme.SetColor("font_disabled_color", "Button", new Color(1f, 1f, 1f, 0.55f));

        theme.SetColor("font_color", "Label", Body);
        theme.AddType(MutedType);
        theme.SetTypeVariation(MutedType, "Label");
        theme.SetColor("font_color", MutedType, Muted);
        return theme;
    }

    public static void Apply(Control root) => root.Theme = Shared;

    public static void ApplyDanger(Button button)
    {
        var fill = ButtonBox(Danger);
        button.AddThemeStyleboxOverride("normal", fill);
        button.AddThemeStyleboxOverride("hover", fill);
        button.AddThemeStyleboxOverride("pressed", fill);
        button.AddThemeStyleboxOverride("focus", fill);
    }

    public static void ApplyMuted(Label label) => label.ThemeTypeVariation = MutedType;

    private static StyleBoxFlat PanelBox(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = CornerRadius,
        CornerRadiusTopRight = CornerRadius,
        CornerRadiusBottomRight = CornerRadius,
        CornerRadiusBottomLeft = CornerRadius,
        BorderWidthLeft = BorderWidth,
        BorderWidthTop = BorderWidth,
        BorderWidthRight = BorderWidth,
        BorderWidthBottom = BorderWidth,
        BorderColor = Border,
        ContentMarginLeft = 24,
        ContentMarginTop = 20,
        ContentMarginRight = 24,
        ContentMarginBottom = 20,
    };

    private static StyleBoxFlat ButtonBox(Color bg) => new()
    {
        BgColor = bg,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomRight = 6,
        CornerRadiusBottomLeft = 6,
        ContentMarginLeft = 12,
        ContentMarginTop = 8,
        ContentMarginRight = 12,
        ContentMarginBottom = 8,
    };

    private static Color Rgb(int rgb, float alpha = 1f) =>
        new((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
}
