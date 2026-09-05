using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Net.Tests.App;

public sealed class PawnLabelPlacementTests
{
    [Fact]
    public void AbovePawn_SitsTwoMetresAboveFeet()
    {
        var at = PawnLabelPlacement.AbovePawn();
        Assert.Equal(0f, at.X);
        Assert.Equal(PawnLabelPlacement.HeightMeters, at.Y);
        Assert.Equal(2f, at.Y);
        Assert.Equal(0f, at.Z);
    }
}

public sealed class PawnPaletteTests
{
    [Fact]
    public void NameFor_UsesPlayerCounter()
    {
        Assert.Equal("Player 3", PawnPalette.NameFor(new EntityId(3)));
    }

    [Fact]
    public void IndexFor_WrapsPaletteCount()
    {
        Assert.Equal((byte)0, PawnPalette.IndexFor(new EntityId(8)));
        Assert.Equal((byte)1, PawnPalette.IndexFor(new EntityId(9)));
    }

    [Fact]
    public void Rgb_ReturnsStableSwatch()
    {
        Assert.Equal((56, 132, 255), PawnPalette.Rgb(0));
        Assert.Equal((236, 240, 241), PawnPalette.Rgb(7));
        Assert.Equal(PawnPalette.Rgb(0), PawnPalette.Rgb(8));
    }
}
