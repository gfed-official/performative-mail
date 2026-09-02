using System;

namespace PerformativeMail.Sim.Mail;

public readonly record struct Cents(int Value);

public sealed class Wallet
{
    // Chapter 11 walletFloor; chapter 03 §2.2 / §4.1: negative only via misdelivery, reject below this.
    public const int Floor = -500;

    public Wallet(Cents balance = default)
    {
        Balance = balance;
    }

    public Cents Balance { get; private set; }

    public void Credit(Cents amount)
    {
        if (amount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Balance = new Cents(checked(Balance.Value + amount.Value));
    }

    // Floor is reject, not clamp: balance - amount < Floor leaves Balance unchanged.
    public bool TryDebit(Cents amount)
    {
        if (amount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        var next = checked(Balance.Value - amount.Value);
        if (next < Floor)
            return false;
        Balance = new Cents(next);
        return true;
    }
}
