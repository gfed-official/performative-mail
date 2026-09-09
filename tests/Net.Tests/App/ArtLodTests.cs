using PerformativeMail.App;

namespace PerformativeMail.Net.Tests.App;

public sealed class ArtLodTests
{
    [Fact]
    public void ForDistance_UsesStyleGuideBands()
    {
        Assert.Equal(30f, ArtLod.Lo1Meters);
        Assert.Equal(60f, ArtLod.Lo2Meters);
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.ForDistance(0f));
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.ForDistance(29.99f));
        Assert.Equal(ArtLodLevel.Lo1, ArtLod.ForDistance(30f));
        Assert.Equal(ArtLodLevel.Lo1, ArtLod.ForDistance(59.99f));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.ForDistance(60f));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.ForDistance(120f));
    }

    [Fact]
    public void Path_InsertsLoSuffixBeforeExtension()
    {
        const string mailbox = "res://art/world/mailbox_01.glb";
        Assert.Equal(mailbox, ArtLod.Path(mailbox, ArtLodLevel.Lo0));
        Assert.Equal("res://art/world/mailbox_01_lo1.glb", ArtLod.Path(mailbox, ArtLodLevel.Lo1));
        Assert.Equal("res://art/world/mailbox_01_lo2.glb", ArtLod.Path(mailbox, ArtLodLevel.Lo2));
        Assert.Equal(
            "res://art/props/crate_01_lo1.glb",
            ArtLod.Path("res://art/props/crate_01.glb", ArtLodLevel.Lo1));
    }

    [Fact]
    public void MaxLevel_MatchesP4MeshBatch()
    {
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/mailbox_01.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/intake_01.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/po_01.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/house_a.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/house_b.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/house_c.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/props/cart_01.glb"));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.MaxLevel("res://art/world/street_pole_01.glb"));
        Assert.Equal(ArtLodLevel.Lo1, ArtLod.MaxLevel("res://art/props/crate_01.glb"));
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.MaxLevel("res://art/world/street_tile_01.glb"));
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.MaxLevel("res://art/world/grass_tile_01.glb"));
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.MaxLevel("res://art/pawns/pawn_remote.glb"));
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.MaxLevel("res://art/props/mail_letter.glb"));
    }

    [Fact]
    public void Resolve_ClampsCrateToLo1PastSixty()
    {
        const string crate = "res://art/props/crate_01.glb";
        Assert.Equal(ArtLodLevel.Lo0, ArtLod.Resolve(10f, crate));
        Assert.Equal(ArtLodLevel.Lo1, ArtLod.Resolve(45f, crate));
        Assert.Equal(ArtLodLevel.Lo1, ArtLod.Resolve(90f, crate));
        Assert.Equal(ArtLodLevel.Lo2, ArtLod.Resolve(90f, "res://art/world/po_01.glb"));
    }

    [Fact]
    public void HorizontalMeters_IgnoresHeight()
    {
        Assert.Equal(30f, ArtLod.HorizontalMeters(0f, 0f, 30f, 0f));
        Assert.Equal(0f, ArtLod.HorizontalMeters(4f, 9f, 4f, 9f));
    }
}
