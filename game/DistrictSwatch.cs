using Godot;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public static class DistrictSwatch
{
    public static Color Of(byte district)
    {
        var (r, g, b) = DistrictPalette.Rgb(DistrictPalette.IndexOf(district));
        return new Color(r / 255f, g / 255f, b / 255f);
    }

    public static string HexOf(byte district) => DistrictPalette.HexOf(district);

    public static string Hex(Color color) =>
        "#" +
        ToByte(color.R).ToString("X2") +
        ToByte(color.G).ToString("X2") +
        ToByte(color.B).ToString("X2");

    private static int ToByte(float channel) =>
        Math.Clamp((int)MathF.Round(channel * 255f), 0, 255);
}
