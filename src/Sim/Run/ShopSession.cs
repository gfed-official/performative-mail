using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Sim.Run;

public readonly record struct ShopOffer(string Id, int Price, int? Remaining, bool OncePerRun);

public enum ShopReject : byte
{
    UnknownItem,
    NotOffered,
    SoldOut,
    AlreadyBought,
    InsufficientFunds,
    Closed,
    NoRoom,
    UnknownGrant
}

public abstract record ShopBuyResult;

public sealed record ShopBought(string Id, Cents Paid, string? Item, int Count, string? Blueprint) : ShopBuyResult;

public sealed record ShopRejected(ShopReject Reason) : ShopBuyResult;

public sealed class ShopSession
{
    public const string RngName = "shop";
    public const int RotatingSlots = 2;
    public const string SpecialTag = "special";

    private readonly ShopItemDef[] _catalog;
    private readonly Dictionary<string, ShopItemDef> _byId;
    private readonly Wallet _wallet;
    private readonly uint _seed;
    private readonly InventorySystem? _inventory;
    private readonly ContainerId _grantTo;
    private readonly IReadOnlyDictionary<string, ItemDefId>? _itemIds;
    private readonly List<ShopOffer> _offers = new();
    private readonly HashSet<string> _bought = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ownedBlueprints = new(StringComparer.Ordinal);
    private bool _open;

    public ShopSession(
        IReadOnlyList<ShopItemDef> catalog,
        Wallet wallet,
        uint seed,
        InventorySystem? inventory = null,
        ContainerId grantTo = default,
        IReadOnlyDictionary<string, ItemDefId>? itemIds = null)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        _catalog = new ShopItemDef[catalog.Count];
        _byId = new Dictionary<string, ShopItemDef>(catalog.Count, StringComparer.Ordinal);
        for (int i = 0; i < catalog.Count; i++)
        {
            var row = catalog[i] ?? throw new ArgumentNullException(nameof(catalog));
            _catalog[i] = row;
            _byId.Add(row.Id, row);
        }

        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _seed = seed;
        _inventory = inventory;
        _grantTo = grantTo;
        _itemIds = itemIds;
    }

    public IReadOnlyList<ShopOffer> Offers => _offers;

    public IReadOnlyCollection<string> OwnedBlueprints => _ownedBlueprints;

    public void RollOffers(byte shift, RunPhase phase = RunPhase.Prep)
    {
        _open = phase is RunPhase.Prep or RunPhase.Payday;
        _offers.Clear();

        var used = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < _catalog.Length; i++)
        {
            var row = _catalog[i];
            if (row.Slot != ShopSlot.Fixed) continue;
            if (!Eligible(row, shift)) continue;
            _offers.Add(ToOffer(row, unlimited: !row.OncePerRun));
            used.Add(row.Id);
        }

        var pool = new List<ShopItemDef>();
        for (int i = 0; i < _catalog.Length; i++)
        {
            var row = _catalog[i];
            if (used.Contains(row.Id)) continue;
            if (!Eligible(row, shift)) continue;
            if (row.Slot != ShopSlot.Rotating && !HasTag(row, SpecialTag)) continue;
            pool.Add(row);
        }

        var rng = RngStream.Derive(_seed, RngName);
        int slots = Math.Min(RotatingSlots, pool.Count);
        for (int n = 0; n < slots; n++)
        {
            int pick = (int)rng.NextBounded((uint)pool.Count);
            var row = pool[pick];
            pool.RemoveAt(pick);
            _offers.Add(ToOffer(row, unlimited: false));
        }
    }

    public ShopBuyResult TryBuy(string shopItemId)
    {
        if (shopItemId is null) throw new ArgumentNullException(nameof(shopItemId));
        if (!_open) return new ShopRejected(ShopReject.Closed);
        if (!_byId.TryGetValue(shopItemId, out var def))
            return new ShopRejected(ShopReject.UnknownItem);

        int index = IndexOfOffer(shopItemId);
        if (index < 0) return new ShopRejected(ShopReject.NotOffered);

        var offer = _offers[index];
        if (offer.OncePerRun && _bought.Contains(shopItemId))
            return new ShopRejected(ShopReject.AlreadyBought);
        if (offer.Remaining == 0)
            return new ShopRejected(ShopReject.SoldOut);
        if (_wallet.Balance.Value < offer.Price)
            return new ShopRejected(ShopReject.InsufficientFunds);

        string? grantItem = def.GrantItem;
        int grantCount = def.GrantCount ?? 1;
        string? grantBlueprint = def.GrantBlueprint;
        ItemDefId itemId = default;
        if (grantItem is null && grantBlueprint is null)
            return new ShopRejected(ShopReject.UnknownGrant);
        if (grantItem is not null)
        {
            if (_inventory is null || _itemIds is null || !_itemIds.TryGetValue(grantItem, out itemId))
                return new ShopRejected(ShopReject.UnknownGrant);
        }

        var paid = new Cents(offer.Price);
        if (!_wallet.TryDebit(paid))
            return new ShopRejected(ShopReject.InsufficientFunds);

        if (grantItem is not null)
        {
            var stack = new ItemStack(itemId, grantCount);
            if (_inventory!.Apply(Actor.System, new Deposit(_grantTo, stack)) is not Accepted)
            {
                _wallet.Credit(paid);
                return new ShopRejected(ShopReject.NoRoom);
            }
        }

        if (offer.Remaining is int left)
            _offers[index] = offer with { Remaining = left - 1 };
        if (offer.OncePerRun)
            _bought.Add(shopItemId);
        if (grantBlueprint is not null)
            _ownedBlueprints.Add(grantBlueprint);

        return new ShopBought(shopItemId, paid, grantItem, grantItem is null ? 0 : grantCount, grantBlueprint);
    }

    private bool Eligible(ShopItemDef row, byte shift)
    {
        if (row.FromShift > shift) return false;
        return !row.OncePerRun || !_bought.Contains(row.Id);
    }

    private static ShopOffer ToOffer(ShopItemDef row, bool unlimited)
        => new(row.Id, row.Price, unlimited ? null : 1, row.OncePerRun);

    private int IndexOfOffer(string id)
    {
        for (int i = 0; i < _offers.Count; i++)
        {
            if (string.Equals(_offers[i].Id, id, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static bool HasTag(ShopItemDef row, string tag)
    {
        for (int i = 0; i < row.Tags.Length; i++)
        {
            if (string.Equals(row.Tags[i], tag, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
