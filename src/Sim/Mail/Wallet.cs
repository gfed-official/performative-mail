using System;

namespace PerformativeMail.Sim.Mail;

public readonly record struct Cents(int Value);

public sealed class Wallet
{
    public Cents Balance { get; private set; }

    public void Credit(Cents amount)
    {
        if (amount.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Balance = new Cents(checked(Balance.Value + amount.Value));
    }
}
