using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Sim.Run;

public sealed class ShopSession
{
    private const string StreamName = "shop";
    private const string SpecialTag = "special";
    private const int SpecialSlots = 2;

    private readonly ShopItemDef[] _catalog;
    private readonly Dictionary<string, ShopItemDef> _byId;
    private readonly Wallet _wallet;
    private readonly uint _seed;
    private readonly InventorySystem? _inventory;
    private readonly ContainerId _grantTo;
    private readonly Dictionary<string, ItemDefId> _itemIds;
    private readonly List<ShopOffer> _offers = new List<ShopOffer>();
    private readonly HashSet<string> _bought = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _blueprints = new HashSet<string>(StringComparer.Ordinal);
    private RunPhase _phase = RunPhase.Prep;

    public ShopSession(
        IReadOnlyList<ShopItemDef> catalog,
        Wallet wallet,
        uint seed,
        InventorySystem? inventory = null,
        ContainerId grantTo = default,
        IReadOnlyDictionary<string, ItemDefId>? itemIds = null)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        _seed = seed;
        _inventory = inventory;
        _grantTo = grantTo;
        _catalog = new ShopItemDef[catalog.Count];
        _byId = new Dictionary<string, ShopItemDef>(catalog.Count, StringComparer.Ordinal);
        for (int i = 0; i < catalog.Count; i++)
        {
            var def = catalog[i] ?? throw new ArgumentNullException(nameof(catalog));
            _catalog[i] = def;
            if (!_byId.ContainsKey(def.Id))
                _byId.Add(def.Id, def);
        }

        _itemIds = new Dictionary<string, ItemDefId>(StringComparer.Ordinal);
        if (itemIds is null) return;
        foreach (var pair in itemIds)
            _itemIds[pair.Key] = pair.Value;
    }

    public IReadOnlyList<ShopOffer> Offers => _offers;

    public IReadOnlyCollection<string> OwnedBlueprints => _blueprints;

    public void RollOffers(byte shift, RunPhase phase = RunPhase.Prep)
    {
        _phase = phase;
        _offers.Clear();
        var offered = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < _catalog.Length; i++)
        {
            var def = _catalog[i];
            if (def.Slot != ShopSlot.Fixed || def.FromShift > shift) continue;
            if (def.OncePerRun && _bought.Contains(def.Id)) continue;
            _offers.Add(ToOffer(def, RemainingOf(def)));
            offered.Add(def.Id);
        }

        var pool = new List<ShopItemDef>();
        for (int i = 0; i < _catalog.Length; i++)
        {
            var def = _catalog[i];
            if (def.FromShift > shift) continue;
            if (def.OncePerRun && _bought.Contains(def.Id)) continue;
            if (offered.Contains(def.Id)) continue;
            if (!IsSpecial(def)) continue;
            pool.Add(def);
        }

        var rng = RngStream.Derive(_seed, StreamName);
        int take = Math.Min(SpecialSlots, pool.Count);
        for (int n = 0; n < take; n++)
        {
            int pick = (int)rng.NextBounded((uint)pool.Count);
            var def = pool[pick];
            pool.RemoveAt(pick);
            _offers.Add(ToOffer(def, remaining: 1));
        }
    }

    public ShopBuyResult TryBuy(string shopItemId)
    {
        if (_phase != RunPhase.Prep && _phase != RunPhase.Payday)
            return new ShopRejected(ShopReject.Closed);
        if (string.IsNullOrEmpty(shopItemId))
            return new ShopRejected(ShopReject.NotOffered);
        if (_bought.Contains(shopItemId))
            return new ShopRejected(ShopReject.AlreadyBought);

        int index = -1;
        ShopOffer offer = default;
        for (int i = 0; i < _offers.Count; i++)
        {
            if (!string.Equals(_offers[i].Id, shopItemId, StringComparison.Ordinal)) continue;
            index = i;
            offer = _offers[i];
            break;
        }

        if (index < 0)
            return new ShopRejected(ShopReject.NotOffered);
        if (offer.Remaining is 0)
            return new ShopRejected(ShopReject.SoldOut);

        if (!CanAfford(offer.Price))
            return new ShopRejected(ShopReject.InsufficientFunds);

        if (!_byId.TryGetValue(shopItemId, out var def))
            return new ShopRejected(ShopReject.NotOffered);

        string? grantItem = def.GrantItem;
        string? grantBlueprint = def.GrantBlueprint;
        string? grantVehicle = def.GrantVehicle;
        int count = def.GrantCount ?? (grantItem is null ? 0 : 1);
        if (grantItem is null && grantBlueprint is null && grantVehicle is null)
            return new ShopRejected(ShopReject.UnknownGrant);

        if (grantItem is not null)
        {
            if (!_itemIds.TryGetValue(grantItem, out var itemId))
                return new ShopRejected(ShopReject.UnknownItem);
            if (_inventory is null)
                return new ShopRejected(ShopReject.NoRoom);
            var deposited = _inventory.Apply(Actor.System, new Deposit(_grantTo, new ItemStack(itemId, count)));
            if (deposited is not Accepted)
                return new ShopRejected(ShopReject.NoRoom);
        }

        if (grantBlueprint is not null)
            _blueprints.Add(grantBlueprint);

        var paid = new Cents(offer.Price);
        _wallet.TryDebit(paid);

        if (offer.OncePerRun)
            _bought.Add(offer.Id);
        if (offer.Remaining is int left)
            _offers[index] = offer with { Remaining = left - 1 };

        return new ShopBought(offer.Id, paid, grantItem, count, grantBlueprint, grantVehicle);
    }

    private bool CanAfford(int price) => _wallet.Balance.Value >= price;

    private static ShopOffer ToOffer(ShopItemDef def, int? remaining)
        => new ShopOffer(def.Id, def.Price, remaining, def.OncePerRun);

    private static int? RemainingOf(ShopItemDef def)
        => def.OncePerRun || IsSpecial(def) ? 1 : (int?)null;

    private static bool IsSpecial(ShopItemDef def)
    {
        if (def.Slot == ShopSlot.Rotating) return true;
        var tags = def.Tags;
        if (tags is null) return false;
        for (int i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i], SpecialTag, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
