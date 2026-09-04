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

public sealed record ShopBought(string Id, Cents Paid, string? Item, int Count, string? Blueprint, string? Vehicle = null) : ShopBuyResult;

public sealed record ShopRejected(ShopReject Reason) : ShopBuyResult;
