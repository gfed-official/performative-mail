using System.IO;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class ShopSessionTests
{
    private static readonly ItemDefId BandageId = new(1);

    [Fact]
    public void TryBuy_OfferedItem_DebitsWalletAndGrantsItem()
    {
        var fx = BandageShop(new Cents(200));
        fx.Shop.RollOffers(1);

        var bought = Assert.IsType<ShopBought>(fx.Shop.TryBuy("bandage_x3"));

        Assert.Equal("bandage_x3", bought.Id);
        Assert.Equal(new Cents(80), bought.Paid);
        Assert.Equal("bandage", bought.Item);
        Assert.Equal(3, bought.Count);
        Assert.Null(bought.Blueprint);
        Assert.Equal(new Cents(120), fx.Wallet.Balance);
        Assert.Equal(3, CountBandages(fx));
    }

    [Fact]
    public void TryBuy_StarterBandage_DebitsWalletAndGrantsItem()
    {
        string path = Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir, "starter.json");
        var defs = ShopCatalog.Parse(File.ReadAllText(path), path);
        var fx = BandageShop(new Cents(200), defs);
        fx.Shop.RollOffers(1);

        Assert.IsType<ShopBought>(fx.Shop.TryBuy("bandage_x3"));
        Assert.Equal(new Cents(120), fx.Wallet.Balance);
        Assert.Equal(3, CountBandages(fx));
    }

    [Fact]
    public void TryBuy_OncePerRun_SecondBuyRejected()
    {
        var defs = ShopCatalog.Parse(
            """
            {
              "id": "bp_sorting",
              "name": "Blueprint: Sorting",
              "kind": "blueprint",
              "price": 400,
              "grants": { "blueprint": "bp_sorting" },
              "availability": { "fromShift": 1, "slot": "fixed" },
              "oncePerRun": true,
              "tags": []
            }
            """,
            "once-per-run");
        var wallet = new Wallet(new Cents(400));
        var shop = new ShopSession(defs, wallet, seed: 1);
        shop.RollOffers(1);

        var first = Assert.IsType<ShopBought>(shop.TryBuy("bp_sorting"));
        Assert.Equal("bp_sorting", first.Blueprint);
        Assert.Equal(new Cents(400), first.Paid);
        Assert.Equal(new Cents(0), wallet.Balance);
        Assert.Contains("bp_sorting", shop.OwnedBlueprints);

        var second = Assert.IsType<ShopRejected>(shop.TryBuy("bp_sorting"));
        Assert.Equal(ShopReject.AlreadyBought, second.Reason);
        Assert.Equal(new Cents(0), wallet.Balance);
        Assert.Single(shop.OwnedBlueprints);
    }

    [Fact]
    public void TryBuy_LastCard_TwoBuys_OneGrant()
    {
        var defs = ShopCatalog.Parse(
            """
            {
              "id": "special_last",
              "name": "Last Card",
              "kind": "item",
              "price": 50,
              "grants": { "item": "bandage", "count": 1 },
              "availability": { "fromShift": 1, "slot": "rotating" },
              "oncePerRun": false,
              "tags": ["special"]
            }
            """,
            "last-card");
        var fx = BandageShop(new Cents(200), defs);
        fx.Shop.RollOffers(1);

        var first = fx.Shop.TryBuy("special_last");
        var second = fx.Shop.TryBuy("special_last");

        Assert.IsType<ShopBought>(first);
        var rejected = Assert.IsType<ShopRejected>(second);
        Assert.True(rejected.Reason is ShopReject.SoldOut or ShopReject.AlreadyBought);
        Assert.Equal(new Cents(150), fx.Wallet.Balance);
        Assert.Equal(1, CountBandages(fx));
    }

    [Fact]
    public void TryBuy_InsufficientFunds_NoGrant()
    {
        var fx = BandageShop(new Cents(79));
        fx.Shop.RollOffers(1);

        var rejected = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bandage_x3"));

        Assert.Equal(ShopReject.InsufficientFunds, rejected.Reason);
        Assert.Equal(new Cents(79), fx.Wallet.Balance);
        Assert.Equal(0, CountBandages(fx));
    }

    [Fact]
    public void TryBuy_DeliveryPhase_Closed()
    {
        var fx = BandageShop(new Cents(200));
        fx.Shop.RollOffers(1, RunPhase.Delivery);

        var rejected = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bandage_x3"));

        Assert.Equal(ShopReject.Closed, rejected.Reason);
        Assert.Equal(new Cents(200), fx.Wallet.Balance);
        Assert.Equal(0, CountBandages(fx));
    }

    [Fact]
    public void TryBuy_Payday_AllowsBuy()
    {
        var fx = BandageShop(new Cents(200));
        fx.Shop.RollOffers(1, RunPhase.Payday);

        Assert.IsType<ShopBought>(fx.Shop.TryBuy("bandage_x3"));
        Assert.Equal(new Cents(120), fx.Wallet.Balance);
        Assert.Equal(3, CountBandages(fx));
    }

    [Fact]
    public void TryBuy_NoRoom_DoesNotDebit()
    {
        var catalog = new BandageStackCatalog();
        var inv = new InventorySystem(catalog);
        var dest = inv.CreateContainer(new ContainerSpec(ContainerShape.Grid(1, 1), null));
        Assert.IsType<Accepted>(inv.Apply(Actor.System, new Deposit(dest, new ItemStack(BandageId, 5))));
        var wallet = new Wallet(new Cents(200));
        var shop = new ShopSession(
            new[] { BandageRow() },
            wallet,
            seed: 1,
            inv,
            dest,
            new Dictionary<string, ItemDefId>(StringComparer.Ordinal) { ["bandage"] = BandageId });
        shop.RollOffers(1);

        var rejected = Assert.IsType<ShopRejected>(shop.TryBuy("bandage_x3"));

        Assert.Equal(ShopReject.NoRoom, rejected.Reason);
        Assert.Equal(new Cents(200), wallet.Balance);
        Assert.Equal(5, CountIn(inv, dest));
    }

    [Fact]
    public void RollOffers_Shift1_OmitsFromShift2()
    {
        var defs = new[]
        {
            BandageRow(),
            ItemRow("oil_can_x3", 160, "oil_can", 3, fromShift: 2)
        };
        var shop = new ShopSession(defs, new Wallet(new Cents(200)), seed: 1);
        shop.RollOffers(1);

        Assert.Contains(shop.Offers, o => o.Id == "bandage_x3");
        Assert.DoesNotContain(shop.Offers, o => o.Id == "oil_can_x3");

        shop.RollOffers(2);
        Assert.Contains(shop.Offers, o => o.Id == "oil_can_x3");
    }

    [Fact]
    public void RollOffers_SameSeed_SameSpecials()
    {
        var defs = new[]
        {
            SpecialRow("special_a", 10),
            SpecialRow("special_b", 20),
            SpecialRow("special_c", 30),
            SpecialRow("special_d", 40)
        };
        const uint seed = 0x51A7EED1;
        var a = new ShopSession(defs, new Wallet(), seed);
        var b = new ShopSession(defs, new Wallet(), seed);
        a.RollOffers(1);
        b.RollOffers(1);

        Assert.Equal(2, a.Offers.Count);
        Assert.Equal(OfferIds(a), OfferIds(b));
    }

    [Fact]
    public void TryBuy_WalletAtMisdeliveryFloor_StillRequiresFullPrice()
    {
        var fx = BandageShop(new Cents(-400));
        fx.Shop.RollOffers(1);

        var rejected = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bandage_x3"));
        Assert.Equal(ShopReject.InsufficientFunds, rejected.Reason);
        Assert.Equal(new Cents(-400), fx.Wallet.Balance);
        Assert.Equal(0, CountBandages(fx));
    }

    [Fact]
    public void RollOffers_UnlimitedFixed_HasNullRemaining()
    {
        var shop = new ShopSession(new[] { BandageRow() }, new Wallet(), seed: 1);
        shop.RollOffers(1);

        var offer = Assert.Single(shop.Offers);
        Assert.Equal("bandage_x3", offer.Id);
        Assert.Null(offer.Remaining);
        Assert.False(offer.OncePerRun);
    }

    private static Fixture BandageShop(Cents balance, IReadOnlyList<ShopItemDef>? defs = null)
    {
        var catalog = new BandageStackCatalog();
        var inv = new InventorySystem(catalog);
        var dest = inv.CreateContainer(ContainerSpec.Chest);
        var wallet = new Wallet(balance);
        var shop = new ShopSession(
            defs ?? new[] { BandageRow() },
            wallet,
            seed: 1,
            inv,
            dest,
            new Dictionary<string, ItemDefId>(StringComparer.Ordinal) { ["bandage"] = BandageId });
        return new Fixture(shop, wallet, inv, dest);
    }

    private static ShopItemDef BandageRow()
        => new(
            "bandage_x3",
            "Bandages ×3",
            ShopKind.Item,
            80,
            "bandage",
            3,
            null,
            null,
            1,
            ShopSlot.Fixed,
            false,
            Array.Empty<string>());

    private static ShopItemDef ItemRow(string id, int price, string grantItem, int count, int fromShift)
        => new(
            id,
            id,
            ShopKind.Item,
            price,
            grantItem,
            count,
            null,
            null,
            fromShift,
            ShopSlot.Fixed,
            false,
            Array.Empty<string>());

    private static ShopItemDef SpecialRow(string id, int price)
        => new(
            id,
            id,
            ShopKind.Item,
            price,
            "bandage",
            1,
            null,
            null,
            1,
            ShopSlot.Rotating,
            false,
            new[] { "special" });

    private static int CountBandages(Fixture fx) => CountIn(fx.Inv, fx.Dest);

    private static int CountIn(InventorySystem inv, ContainerId dest)
    {
        Assert.True(inv.TryGetContainer(dest, out var grid));
        int n = 0;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is ItemStack item && item.Item.Equals(BandageId))
                n += item.Count;
        }

        return n;
    }

    private static string[] OfferIds(ShopSession shop)
    {
        var ids = new string[shop.Offers.Count];
        for (int i = 0; i < shop.Offers.Count; i++)
            ids[i] = shop.Offers[i].Id;
        return ids;
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, ArchetypeCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/archetypes.json");
    }

    private readonly record struct Fixture(
        ShopSession Shop,
        Wallet Wallet,
        InventorySystem Inv,
        ContainerId Dest);

    private sealed class BandageStackCatalog : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key)
        {
            if (!key.IsMail && key.Def == BandageId.Value) return new Footprint(1, 1);
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public int MaxStackOf(StackKey key)
        {
            if (!key.IsMail && key.Def == BandageId.Value) return 5;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public WeightClass WeightOf(StackKey key)
        {
            if (!key.IsMail && key.Def == BandageId.Value) return WeightClass.Light;
            throw new ArgumentException("Unknown stack key.", nameof(key));
        }

        public StackCategory CategoryOf(StackKey key) => StackCategory.Consumable;
    }
}
