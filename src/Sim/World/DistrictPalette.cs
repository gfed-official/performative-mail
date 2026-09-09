using System;

namespace PerformativeMail.Sim.World;

public enum DistrictPattern : byte
{
    Solid = 0,
    DiagonalStripe = 1,
    Dots = 2,
    Chevron = 3,
    HorizontalStripe = 4,
    Crosshatch = 5,
    VerticalStripe = 6,
    OutlineOnly = 7,
}

public static class DistrictPalette
{
    public const int Count = 8;

    public static bool HasSwatch(byte district) => district != 0;

    public static byte IndexOf(byte district) =>
        district == 0 ? (byte)0 : (byte)((district - 1) % Count);

    public static bool TryIndex(byte district, out byte index)
    {
        if (district == 0)
        {
            index = 0;
            return false;
        }

        index = (byte)((district - 1) % Count);
        return true;
    }

    public static (byte R, byte G, byte B) Rgb(byte index)
    {
        switch (index % Count)
        {
            case 0: return (0x3D, 0x7E, 0xFF);
            case 1: return (0xE8, 0x5D, 0x3A);
            case 2: return (0x2E, 0xCC, 0x71);
            case 3: return (0xF1, 0xC4, 0x0F);
            case 4: return (0x9B, 0x59, 0xB6);
            case 5: return (0x1A, 0xBC, 0x9C);
            case 6: return (0xE6, 0x7E, 0x22);
            case 7: return (0xEC, 0xF0, 0xF1);
            default:
                throw new ArgumentOutOfRangeException(nameof(index), index, null);
        }
    }

    public static DistrictPattern Pattern(byte index) => (DistrictPattern)(index % Count);

    public static string Hex(byte index)
    {
        var (r, g, b) = Rgb(index);
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    public static string HexOf(byte district) => Hex(IndexOf(district));
}
