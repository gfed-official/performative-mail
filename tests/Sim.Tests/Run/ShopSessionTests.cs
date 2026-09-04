using System;
using System.Collections.Generic;
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
    private static readonly ItemDefId Bandage = new(2);
    private const uint Seed = 0x7F3A9C21;

    [Fact]
    public void TryBuy_OfferedItem_DebitsWalletAndGrantsItem()
    {
        var fx = Fixture.WithStarter();
        fx.Shop.RollOffers(1);

        var bought = Assert.IsType<ShopBought>(fx.Shop.TryBuy("bandage_x3"));

        Assert.Equal("bandage_x3", bought.Id);
        Assert.Equal(new Cents(80), bought.Paid);
        Assert.Equal("bandage", bought.Item);
        Assert.Equal(3, bought.Count);
        Assert.Equal(new Cents(120), fx.Wallet.Balance);
        Assert.Equal(3, fx.Count(Bandage));
    }

    [Fact]
    public void TryBuy_OncePerRun_SecondBuyRejected()
    {
        var fx = Fixture.With(Blueprint("bp_sorting", 400), Wallet: new Wallet(new Cents(800)));
        fx.Shop.RollOffers(1);

        var first = Assert.IsType<ShopBought>(fx.Shop.TryBuy("bp_sorting"));
        Assert.Equal("bp_sorting", first.Blueprint);
        Assert.Contains("bp_sorting", fx.Shop.OwnedBlueprints);
        Assert.Equal(new Cents(400), fx.Wallet.Balance);

        var second = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bp_sorting"));
        Assert.Equal(ShopReject.AlreadyBought, second.Reason);
        Assert.Equal(new Cents(400), fx.Wallet.Balance);
        Assert.Single(fx.Shop.OwnedBlueprints);
    }

    [Fact]
    public void TryBuy_LastCard_TwoBuys_OneGrant()
    {
        var fx = Fixture.With(
            Item("special_last", 50, "bandage", 1, ShopSlot.Rotating, once: false, "special"),
            Wallet: new Wallet(new Cents(200)));
        fx.Shop.RollOffers(1);

        var first = fx.Shop.TryBuy("special_last");
        var second = fx.Shop.TryBuy("special_last");

        Assert.IsType<ShopBought>(first);
        var reject = Assert.IsType<ShopRejected>(second);
        Assert.True(reject.Reason is ShopReject.SoldOut or ShopReject.AlreadyBought);
        Assert.Equal(new Cents(150), fx.Wallet.Balance);
        Assert.Equal(1, fx.Count(Bandage));
    }

    [Fact]
    public void TryBuy_InsufficientFunds_DoesNotGrant()
    {
        var fx = Fixture.WithStarter(new Wallet(new Cents(79)));
        fx.Shop.RollOffers(1);

        var rejected = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bandage_x3"));
        Assert.Equal(ShopReject.InsufficientFunds, rejected.Reason);
        Assert.Equal(new Cents(79), fx.Wallet.Balance);
        Assert.Equal(0, fx.Count(Bandage));
    }

    [Fact]
    public void TryBuy_DeliveryPhase_Closed()
    {
        var fx = Fixture.WithStarter();
        fx.Shop.RollOffers(1, RunPhase.Delivery);

        var rejected = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bandage_x3"));
        Assert.Equal(ShopReject.Closed, rejected.Reason);
        Assert.Equal(new Cents(200), fx.Wallet.Balance);
    }

    [Fact]
    public void RollOffers_Shift1_OmitsFromShift2()
    {
        var fx = Fixture.WithStarter();
        fx.Shop.RollOffers(1);

        Assert.DoesNotContain(fx.Shop.Offers, o => o.Id == "oil_can_x3");
        Assert.Contains(fx.Shop.Offers, o => o.Id == "bandage_x3");
    }

    [Fact]
    public void RollOffers_SameSeed_SameSpecials()
    {
        var defs = new[]
        {
            Item("special_a", 10, "bandage", 1, ShopSlot.Rotating, once: false, "special"),
            Item("special_b", 10, "bandage", 1, ShopSlot.Rotating, once: false, "special"),
            Item("special_c", 10, "bandage", 1, ShopSlot.Rotating, once: false, "special")
        };

        var a = Fixture.With(defs);
        var b = Fixture.With(defs);
        a.Shop.RollOffers(1);
        b.Shop.RollOffers(1);

        Assert.Equal(Ids(a.Shop.Offers), Ids(b.Shop.Offers));
        Assert.Equal(2, a.Shop.Offers.Count);
    }

    [Fact]
    public void TryBuy_WalletAtMisdeliveryFloor_StillRequiresFullPrice()
    {
        var fx = Fixture.WithStarter(new Wallet(new Cents(-400)));
        fx.Shop.RollOffers(1);

        var rejected = Assert.IsType<ShopRejected>(fx.Shop.TryBuy("bandage_x3"));
        Assert.Equal(ShopReject.InsufficientFunds, rejected.Reason);
        Assert.Equal(new Cents(-400), fx.Wallet.Balance);
    }

    private static List<string> Ids(IReadOnlyList<ShopOffer> offers)
    {
        var ids = new List<string>(offers.Count);
        for (int i = 0; i < offers.Count; i++)
            ids.Add(offers[i].Id);
        return ids;
    }

    private static ShopItemDef Item(
        string id,
        int price,
        string grant,
        int count,
        ShopSlot slot,
        bool once,
        params string[] tags)
    {
        return new ShopItemDef(
            id,
            id,
            ShopKind.Item,
            price,
            grant,
            count,
            null,
            null,
            1,
            slot,
            once,
            tags);
    }

    private static ShopItemDef Blueprint(string id, int price)
    {
        return new ShopItemDef(
            id,
            id,
            ShopKind.Blueprint,
            price,
            null,
            null,
            id,
            null,
            1,
            ShopSlot.Fixed,
            true,
            Array.Empty<string>());
    }

    private sealed class Fixture
    {
        private Fixture(ShopItemDef[] defs, Wallet wallet)
        {
            Wallet = wallet;
            Catalog = new ShopItems();
            Inv = new InventorySystem(Catalog);
            GrantTo = Inv.CreateContainer(ContainerSpec.Depot);
            ItemIds = new Dictionary<string, ItemDefId>(StringComparer.Ordinal)
            {
                ["bandage"] = Bandage
            };
            Shop = new ShopSession(defs, Wallet, Seed, Inv, GrantTo, ItemIds);
        }

        public static Fixture WithStarter(Wallet? wallet = null)
        {
            var defs = ShopCatalog.LoadDir(Path.Combine(FindContentRoot(), ShopCatalog.RelativeDir));
            return new Fixture(defs, wallet ?? new Wallet(new Cents(200)));
        }

        public static Fixture With(params ShopItemDef[] defs) => new(defs, new Wallet(new Cents(200)));

        public static Fixture With(ShopItemDef def, Wallet Wallet) => new(new[] { def }, Wallet);

        public Wallet Wallet { get; }

        public ShopItems Catalog { get; }

        public InventorySystem Inv { get; }

        public ContainerId GrantTo { get; }

        public IReadOnlyDictionary<string, ItemDefId> ItemIds { get; }

        public ShopSession Shop { get; }

        public int Count(ItemDefId item)
        {
            int n = 0;
            Assert.True(Inv.TryGetContainer(GrantTo, out var grid));
            foreach (var entry in grid.Entries)
            {
                if (entry.Stack is ItemStack stack && stack.Item.Equals(item))
                    n += stack.Count;
            }

            return n;
        }
    }

    private sealed class ShopItems : IStackCatalog
    {
        public Footprint FootprintOf(StackKey key) => new(1, 1);

        public int MaxStackOf(StackKey key) => 5;

        public WeightClass WeightOf(StackKey key) => WeightClass.Light;

        public StackCategory CategoryOf(StackKey key) => StackCategory.Consumable;
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
}
