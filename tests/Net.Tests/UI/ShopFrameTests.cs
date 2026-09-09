using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests.UI;

public sealed class ShopFrameTests
{
    [Fact]
    public void Inspect_ListsCatalogPrices()
    {
        var frame = ShopBoot.Inspect();

        Assert.Equal("$10.00", frame.WalletLabel);
        Assert.Equal("PREP", frame.PhaseLabel);
        Assert.Equal(4, frame.Rows.Count);
        Assert.Equal("axe", frame.Rows[0].Id);
        Assert.Equal("Axe", frame.Rows[0].NameLabel);
        Assert.Equal("$0.80", frame.Rows[0].PriceLabel);
        Assert.True(frame.Rows[0].CanBuy);
        Assert.Equal("bandage_x3", frame.Rows[1].Id);
        Assert.Equal("$0.80", frame.Rows[1].PriceLabel);
        Assert.Equal("bike", frame.Rows[2].Id);
        Assert.Equal("$1.20", frame.Rows[2].PriceLabel);
        Assert.Equal("bp_pipes", frame.Rows[3].Id);
        Assert.Equal("$7.00", frame.Rows[3].PriceLabel);
        Assert.Equal(ShopFrame.UnlocksPrefix + "3", frame.Rows[3].TagLabel);
        Assert.False(frame.Rows[3].CanBuy);
    }

    [Fact]
    public void From_Catalog_UsesOfferStateAndWallet()
    {
        var catalog = new[]
        {
            Item("oil_can_x3", "Oil Cans ×3", 160, fromShift: 2),
            Item("bandage_x3", "Bandages ×3", 80, fromShift: 1),
            Item("bp_pipes", "Blueprint: Pneumatics", 700, fromShift: 3, once: true),
        };
        var offers = new[]
        {
            new ShopOffer("bandage_x3", 80, Remaining: null, OncePerRun: false),
        };

        var frame = ShopFrame.From(catalog, offers, new Cents(80), RunPhase.Prep, shift: 1);

        Assert.Equal("$0.80", frame.WalletLabel);
        Assert.Equal("PREP", frame.PhaseLabel);
        Assert.Equal("Bandages ×3", frame.Rows[0].NameLabel);
        Assert.True(frame.Rows[0].CanBuy);
        Assert.Equal("Blueprint: Pneumatics", frame.Rows[1].NameLabel);
        Assert.Equal(ShopFrame.UnlocksPrefix + "3", frame.Rows[1].TagLabel);
        Assert.False(frame.Rows[1].CanBuy);
        Assert.Equal("Oil Cans ×3", frame.Rows[2].NameLabel);
        Assert.Equal(ShopFrame.UnlocksPrefix + "2", frame.Rows[2].TagLabel);
        Assert.False(frame.Rows[2].CanBuy);
    }

    [Fact]
    public void From_Delivery_DisablesBuy()
    {
        var catalog = new[] { Item("bandage_x3", "Bandages ×3", 80, fromShift: 1) };
        var offers = new[] { new ShopOffer("bandage_x3", 80, Remaining: null, OncePerRun: false) };

        var frame = ShopFrame.From(catalog, offers, new Cents(200), RunPhase.Delivery, shift: 1);

        Assert.Equal("DELIVERY", frame.PhaseLabel);
        Assert.False(Assert.Single(frame.Rows).CanBuy);
        Assert.False(ShopFrame.PhaseOpen(RunPhase.Delivery));
        Assert.True(ShopFrame.PhaseOpen(RunPhase.Payday));
    }

    [Fact]
    public void SameDisplay_IgnoresIdenticalCatalog()
    {
        var frame = ShopBoot.Inspect();
        Assert.True(ShopFrame.SameDisplay(in frame, in frame));
        var other = frame with { WalletLabel = "$9.20" };
        Assert.False(ShopFrame.SameDisplay(in frame, in other));
    }

    [Fact]
    public void From_BrokeWallet_LocksAffordableFlag()
    {
        var catalog = new[] { Item("bandage_x3", "Bandages ×3", 80, fromShift: 1) };
        var offers = new[] { new ShopOffer("bandage_x3", 80, Remaining: null, OncePerRun: false) };

        var frame = ShopFrame.From(catalog, offers, new Cents(79), RunPhase.Prep, shift: 1);

        Assert.False(Assert.Single(frame.Rows).CanBuy);
        Assert.Equal("$0.79", frame.WalletLabel);
    }

    private static ShopItemDef Item(string id, string name, int price, int fromShift, bool once = false)
        => new(
            id,
            name,
            ShopKind.Item,
            price,
            "bandage",
            1,
            null,
            null,
            fromShift,
            ShopSlot.Fixed,
            once,
            Array.Empty<string>());
}
