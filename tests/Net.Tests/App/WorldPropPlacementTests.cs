using PerformativeMail.App;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

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
    public void MailboxFlagOffset_PlusXStreet_SitsOutsidePost()
    {
        var (x, y, z) = WorldPropPlacement.MailboxFlagOffset(0.28f, 0.28f, 4f, 1f);
        Assert.Equal(WorldPropPlacement.MailboxFlagOutboard(0.28f), x);
        Assert.Equal(WorldPropPlacement.MailboxFlagCenterY, y);
        Assert.Equal(0f, z);
        Assert.True(x > 0.14f);
    }

    [Fact]
    public void MailboxFlagOffset_PlusZStreet_SitsOutsidePost()
    {
        var (x, y, z) = WorldPropPlacement.MailboxFlagOffset(0.28f, 0.28f, 1f, -3f);
        Assert.Equal(0f, x);
        Assert.Equal(WorldPropPlacement.MailboxFlagCenterY, y);
        Assert.Equal(-WorldPropPlacement.MailboxFlagOutboard(0.28f), z);
        Assert.True(z < -0.14f);
    }

    [Fact]
    public void MailboxFlagOffset_NoStreet_DefaultsPlusX()
    {
        var (x, y, z) = WorldPropPlacement.MailboxFlagOffset(0.28f, 0.28f, 0f, 0f);
        Assert.Equal(WorldPropPlacement.MailboxFlagOutboard(0.28f), x);
        Assert.Equal(0f, z);
        Assert.Equal(WorldPropPlacement.MailboxFlagCenterY, y);
    }

    [Fact]
    public void MailboxFlagSize_OrientsLengthAlongStreet()
    {
        var alongX = WorldPropPlacement.MailboxFlagSize(2f, 1f);
        Assert.Equal(WorldPropPlacement.MailboxFlagLengthMeters, alongX.X);
        Assert.Equal(WorldPropPlacement.MailboxFlagHeightMeters, alongX.Y);
        Assert.Equal(WorldPropPlacement.MailboxFlagThicknessMeters, alongX.Z);

        var alongZ = WorldPropPlacement.MailboxFlagSize(1f, 3f);
        Assert.Equal(WorldPropPlacement.MailboxFlagThicknessMeters, alongZ.X);
        Assert.Equal(WorldPropPlacement.MailboxFlagHeightMeters, alongZ.Y);
        Assert.Equal(WorldPropPlacement.MailboxFlagLengthMeters, alongZ.Z);
    }

    [Fact]
    public void MailboxFlag_AabbClearsThePost()
    {
        AssertFlagClearsBody(4f, 1f);
        AssertFlagClearsBody(1f, -3f);
        AssertFlagClearsBody(0f, 0f);
    }

    [Fact]
    public void DebugMailboxFlag_SticksOutTowardStreetAndClearsPost()
    {
        var tables = DebugWorld.Tables();
        float tileM = tables.TileCm / 100f;
        var house = tables.Houses[0];
        var view = ViewFrame.From(new PlayerPose(house.Mailbox.XCm, house.Mailbox.YCm, house.Mailbox.ZCm, 0));
        var toward = WorldTilePlacement.TowardNearestStreet(view.X, view.Z, tables.Streets, tileM);
        Assert.True(MathF.Abs(toward.Z) > MathF.Abs(toward.X));
        Assert.True(toward.Z > 0f);
        AssertFlagClearsBody(toward.X, toward.Z);
        var size = WorldPropPlacement.MailboxFlagSize(toward.X, toward.Z);
        Assert.Equal(WorldPropPlacement.MailboxFlagLengthMeters, size.Z);
        Assert.Equal(WorldPropPlacement.MailboxFlagThicknessMeters, size.X);
    }

    private static void AssertFlagClearsBody(float towardX, float towardZ)
    {
        const float body = 0.28f;
        var size = WorldPropPlacement.MailboxFlagSize(towardX, towardZ);
        var at = WorldPropPlacement.MailboxFlagOffset(body, body, towardX, towardZ);
        float bodyHalf = body * 0.5f;
        float minX = at.X - size.X * 0.5f;
        float maxX = at.X + size.X * 0.5f;
        float minZ = at.Z - size.Z * 0.5f;
        float maxZ = at.Z + size.Z * 0.5f;
        bool overlapX = minX < bodyHalf && maxX > -bodyHalf;
        bool overlapZ = minZ < bodyHalf && maxZ > -bodyHalf;
        Assert.False(overlapX && overlapZ);
        float inner = MathF.Abs(towardZ) > MathF.Abs(towardX)
            ? MathF.Abs(at.Z) - size.Z * 0.5f
            : MathF.Abs(at.X) - size.X * 0.5f;
        Assert.True(inner > bodyHalf);
    }
}
