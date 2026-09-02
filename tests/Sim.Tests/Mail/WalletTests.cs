using System;
using PerformativeMail.Sim.Mail;

namespace PerformativeMail.Sim.Tests.Mail;

public sealed class WalletTests
{
    [Fact]
    public void TryDebit_FourFromZero_BalanceNegativeFour()
    {
        var wallet = new Wallet();

        Assert.True(wallet.TryDebit(new Cents(4)));

        Assert.Equal(new Cents(-4), wallet.Balance);
    }

    [Fact]
    public void TryDebit_FourAtFloor_ReturnsFalseAndUnchanged()
    {
        var wallet = new Wallet(new Cents(-500));

        Assert.False(wallet.TryDebit(new Cents(4)));

        Assert.Equal(new Cents(-500), wallet.Balance);
    }

    [Fact]
    public void TryDebit_FourFromNegative497_ReturnsFalseAndUnchanged()
    {
        var wallet = new Wallet(new Cents(-497));

        Assert.False(wallet.TryDebit(new Cents(4)));

        Assert.Equal(new Cents(-497), wallet.Balance);
    }

    [Fact]
    public void TryDebit_FourFromNegative496_LandsOnFloor()
    {
        var wallet = new Wallet(new Cents(-496));

        Assert.True(wallet.TryDebit(new Cents(4)));

        Assert.Equal(new Cents(-500), wallet.Balance);
    }

    [Fact]
    public void TryDebit_NegativeAmount_Throws()
    {
        var wallet = new Wallet();

        Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TryDebit(new Cents(-1)));
        Assert.Equal(new Cents(0), wallet.Balance);
    }
}
