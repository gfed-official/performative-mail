using PerformativeMail.App;

namespace PerformativeMail.Net.Tests.App;

public sealed class WorldLabelPlacementTests
{
    [Fact]
    public void AboveStreetFace_ZeroToward_SitsAboveRoofCenter()
    {
        var (x, y, z) = WorldLabelPlacement.AboveStreetFace(12f, 2.4f, 12f, 1.2f, 0f, 0f);
        Assert.Equal(0f, x);
        Assert.Equal(0f, z);
        Assert.Equal(2.4f + WorldLabelPlacement.RoofClearanceMeters, y);
        Assert.True(y > 2.4f);
    }

    [Fact]
    public void AboveStreetFace_TowardNorth_SitsOutsideAndAbove()
    {
        var (x, y, z) = WorldLabelPlacement.AboveStreetFace(12f, 2.4f, 12f, 1.2f, 0f, -8f);
        Assert.Equal(0f, x);
        Assert.Equal(2.4f + WorldLabelPlacement.RoofClearanceMeters, y);
        Assert.Equal(-(6f + WorldLabelPlacement.FaceClearanceMeters), z);
        Assert.True(z < -6f);
    }

    [Fact]
    public void AboveStreetFace_TowardEast_SitsOutsideAndAbove()
    {
        var (x, y, z) = WorldLabelPlacement.AboveStreetFace(5.6f, 1.8f, 5.6f, 0.9f, 3f, 1f);
        Assert.Equal(2.8f + WorldLabelPlacement.FaceClearanceMeters, x);
        Assert.Equal(1.8f + WorldLabelPlacement.RoofClearanceMeters, y);
        Assert.Equal(0f, z);
        Assert.True(x > 2.8f);
        Assert.True(y > 1.8f);
    }

    [Fact]
    public void AboveStreetFace_Mailbox_ClearsThePost()
    {
        var (x, y, z) = WorldLabelPlacement.AboveStreetFace(0.28f, 1.15f, 0.28f, 0.57f, 0f, 0f);
        Assert.Equal(0f, x);
        Assert.Equal(0f, z);
        Assert.Equal(1.145f + WorldLabelPlacement.RoofClearanceMeters, y, 3);
        Assert.True(y > 1.15f);
    }

    [Fact]
    public void AboveStreetFace_HouseWithRoof_ClearsStackedRoof()
    {
        float bodyY = 1.8f;
        float heightCenter = 0.9f;
        float stack = WorldPropPlacement.HouseRoofHeightMeters;
        var (_, y, _) = WorldLabelPlacement.AboveStreetFace(
            5.6f,
            bodyY + stack,
            5.6f,
            heightCenter + stack * 0.5f,
            0f,
            0f);
        float roofTop = bodyY + stack;
        Assert.True(y > roofTop);
        Assert.Equal(roofTop + WorldLabelPlacement.RoofClearanceMeters, y);
    }
}
