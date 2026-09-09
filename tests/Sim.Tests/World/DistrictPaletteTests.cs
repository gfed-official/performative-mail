using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.World;

public sealed class DistrictPaletteTests
{
    [Theory]
    [InlineData(1, 0, "#3D7EFF", DistrictPattern.Solid)]
    [InlineData(2, 1, "#E85D3A", DistrictPattern.DiagonalStripe)]
    [InlineData(3, 2, "#2ECC71", DistrictPattern.Dots)]
    [InlineData(4, 3, "#F1C40F", DistrictPattern.Chevron)]
    [InlineData(5, 4, "#9B59B6", DistrictPattern.HorizontalStripe)]
    [InlineData(6, 5, "#1ABC9C", DistrictPattern.Crosshatch)]
    [InlineData(7, 6, "#E67E22", DistrictPattern.VerticalStripe)]
    [InlineData(8, 7, "#ECF0F1", DistrictPattern.OutlineOnly)]
    public void StyleGuideSlots_MapOneBasedDistricts(byte district, byte index, string hex, DistrictPattern pattern)
    {
        Assert.True(DistrictPalette.HasSwatch(district));
        Assert.True(DistrictPalette.TryIndex(district, out byte mapped));
        Assert.Equal(index, mapped);
        Assert.Equal(index, DistrictPalette.IndexOf(district));
        Assert.Equal(hex, DistrictPalette.Hex(index));
        Assert.Equal(hex, DistrictPalette.HexOf(district));
        Assert.Equal(pattern, DistrictPalette.Pattern(index));
    }

    [Fact]
    public void Rgb_MatchesLockedHexBytes()
    {
        Assert.Equal((0x3D, 0x7E, 0xFF), DistrictPalette.Rgb(0));
        Assert.Equal((0xE8, 0x5D, 0x3A), DistrictPalette.Rgb(1));
        Assert.Equal((0x2E, 0xCC, 0x71), DistrictPalette.Rgb(2));
        Assert.Equal((0xF1, 0xC4, 0x0F), DistrictPalette.Rgb(3));
        Assert.Equal((0x9B, 0x59, 0xB6), DistrictPalette.Rgb(4));
        Assert.Equal((0x1A, 0xBC, 0x9C), DistrictPalette.Rgb(5));
        Assert.Equal((0xE6, 0x7E, 0x22), DistrictPalette.Rgb(6));
        Assert.Equal((0xEC, 0xF0, 0xF1), DistrictPalette.Rgb(7));
        Assert.Equal(DistrictPalette.Rgb(0), DistrictPalette.Rgb(8));
    }

    [Fact]
    public void IndexOf_WrapsAfterEightDistricts()
    {
        Assert.Equal((byte)0, DistrictPalette.IndexOf(9));
        Assert.Equal((byte)1, DistrictPalette.IndexOf(10));
        Assert.Equal("#3D7EFF", DistrictPalette.HexOf(9));
    }

    [Fact]
    public void DistrictZero_HasNoSwatch()
    {
        Assert.False(DistrictPalette.HasSwatch(0));
        Assert.False(DistrictPalette.TryIndex(0, out byte index));
        Assert.Equal((byte)0, index);
        Assert.Equal((byte)0, DistrictPalette.IndexOf(0));
    }
}
