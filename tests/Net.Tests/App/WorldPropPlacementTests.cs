using PerformativeMail.App;

namespace PerformativeMail.Net.Tests.App;

public sealed class WorldPropPlacementTests
{
    [Fact]
    public void RoofSize_IsNinetyPercentFootprintAndFixedHeight()
    {
        var (x, y, z) = WorldPropPlacement.RoofSize(8f, 6f);
        Assert.Equal(8f * WorldPropPlacement.HouseRoofScale, x);
        Assert.Equal(WorldPropPlacement.HouseRoofHeightMeters, y);
        Assert.Equal(6f * WorldPropPlacement.HouseRoofScale, z);
        Assert.Equal(0.55f, WorldPropPlacement.HouseRoofHeightMeters);
        Assert.Equal(0.9f, WorldPropPlacement.HouseRoofScale);
    }

    [Fact]
    public void RoofCenterY_StacksOnBody()
    {
        Assert.Equal(2.075f, WorldPropPlacement.RoofCenterY(1.8f));
    }

    [Fact]
    public void MailboxFlagOffset_PlusXStreet_SitsOnStreetFace()
    {
        var (x, y, z) = WorldPropPlacement.MailboxFlagOffset(0.28f, 0.28f, 4f, 1f);
        Assert.Equal(0.15f, x);
        Assert.Equal(WorldPropPlacement.MailboxFlagCenterY, y);
        Assert.Equal(0f, z);
    }

    [Fact]
    public void MailboxFlagOffset_PlusZStreet_SitsOnStreetFace()
    {
        var (x, y, z) = WorldPropPlacement.MailboxFlagOffset(0.28f, 0.28f, 1f, -3f);
        Assert.Equal(0f, x);
        Assert.Equal(WorldPropPlacement.MailboxFlagCenterY, y);
        Assert.Equal(-0.15f, z);
    }

    [Fact]
    public void MailboxFlagOffset_NoStreet_DefaultsPlusX()
    {
        var (x, y, z) = WorldPropPlacement.MailboxFlagOffset(0.28f, 0.28f, 0f, 0f);
        Assert.Equal(0.15f, x);
        Assert.Equal(0f, z);
        Assert.Equal(0.95f, y);
    }

    [Fact]
    public void MailboxFlagSize_OrientsThicknessAlongStreet()
    {
        var alongX = WorldPropPlacement.MailboxFlagSize(2f, 1f);
        Assert.Equal(0.02f, alongX.X);
        Assert.Equal(0.12f, alongX.Y);
        Assert.Equal(0.22f, alongX.Z);

        var alongZ = WorldPropPlacement.MailboxFlagSize(1f, 3f);
        Assert.Equal(0.22f, alongZ.X);
        Assert.Equal(0.12f, alongZ.Y);
        Assert.Equal(0.02f, alongZ.Z);
    }
}
