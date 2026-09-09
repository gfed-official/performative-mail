using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct ShopRowFrame(
    string Id,
    string NameLabel,
    string PriceLabel,
    string TagLabel,
    bool CanBuy);

public readonly record struct ShopFrame(
    string WalletLabel,
    string PhaseLabel,
    IReadOnlyList<ShopRowFrame> Rows)
{
    public const string ClosedText = "Closed";
    public const string UnlocksPrefix = "Unlocks shift ";
    public const string NotOfferedText = "Not offered";
    public const string BoughtText = "Bought";
    public const string SoldOutText = "Sold out";
    public const string OncePerRunText = "Once per run";
    public const string SpecialText = "Special";

    public static bool PhaseOpen(RunPhase phase) =>
        phase is RunPhase.Prep or RunPhase.Payday;

    public static bool SameDisplay(in ShopFrame a, in ShopFrame b)
    {
        if (a.WalletLabel != b.WalletLabel || a.PhaseLabel != b.PhaseLabel)
            return false;
        if (ReferenceEquals(a.Rows, b.Rows))
            return true;
        if (a.Rows is null || b.Rows is null || a.Rows.Count != b.Rows.Count)
            return false;
        for (int i = 0; i < a.Rows.Count; i++)
        {
            if (!a.Rows[i].Equals(b.Rows[i]))
                return false;
        }

        return true;
    }

    public static ShopFrame From(
        IReadOnlyList<ShopItemDef> catalog,
        IReadOnlyList<ShopOffer> offers,
        Cents wallet,
        RunPhase phase,
        byte shift)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (offers is null) throw new ArgumentNullException(nameof(offers));

        var byId = new Dictionary<string, ShopOffer>(offers.Count, StringComparer.Ordinal);
        for (int i = 0; i < offers.Count; i++)
        {
            var offer = offers[i];
            if (!byId.ContainsKey(offer.Id))
                byId.Add(offer.Id, offer);
        }

        var rows = new ShopRowFrame[catalog.Count];
        var order = new int[catalog.Count];
        for (int i = 0; i < catalog.Count; i++)
            order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            int names = string.Compare(catalog[a].Name, catalog[b].Name, StringComparison.Ordinal);
            return names != 0 ? names : string.Compare(catalog[a].Id, catalog[b].Id, StringComparison.Ordinal);
        });

        bool open = PhaseOpen(phase);
        for (int i = 0; i < order.Length; i++)
        {
            var def = catalog[order[i]];
            byId.TryGetValue(def.Id, out var offer);
            bool offered = byId.ContainsKey(def.Id);
            string tag = Tag(def, offered, offer, shift);
            bool canBuy = open
                && offered
                && def.FromShift <= shift
                && offer.Remaining != 0
                && wallet.Value >= def.Price;
            rows[i] = new ShopRowFrame(
                def.Id,
                def.Name,
                FormatCents(def.Price),
                tag,
                canBuy);
        }

        return new ShopFrame(FormatCents(wallet.Value), PhaseLabelOf(phase), rows);
    }

    public static string FormatCents(int value)
    {
        int abs = value < 0 ? -value : value;
        string amount = $"${abs / 100}.{abs % 100:D2}";
        return value < 0 ? "-" + amount : amount;
    }

    public static string PhaseLabelOf(RunPhase phase) => phase switch
    {
        RunPhase.Lobby => "LOBBY",
        RunPhase.Generating => "GENERATING",
        RunPhase.Prep => "PREP",
        RunPhase.Delivery => "DELIVERY",
        RunPhase.Raid => "RAID",
        RunPhase.Payday => "PAYDAY",
        RunPhase.Draft => "DRAFT",
        RunPhase.Results => "RESULTS",
        RunPhase.RunOver => "RUN OVER",
        RunPhase.Victory => "VICTORY",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    private static string Tag(ShopItemDef def, bool offered, in ShopOffer offer, byte shift)
    {
        if (def.FromShift > shift)
            return UnlocksPrefix + def.FromShift.ToString();
        if (!offered)
            return def.OncePerRun ? BoughtText : NotOfferedText;
        if (offer.Remaining is 0)
            return SoldOutText;
        if (def.OncePerRun)
            return OncePerRunText;
        if (def.Slot == ShopSlot.Rotating)
            return SpecialText;
        return "";
    }
}
