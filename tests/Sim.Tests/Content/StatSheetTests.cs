using PerformativeMail.Sim.Content;

namespace PerformativeMail.Sim.Tests.Content;

public sealed class StatSheetTests
{
    [Fact]
    public void Get_TwoMulsAndOneAdd_BeltSpeedMatchesProductThenSum()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 1.5));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 2));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Add, 4));

        Assert.Equal(34, sheet.Get(Stat.BeltSpeed));
        Assert.NotEqual(10 * (1.5 + 2) + 4, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_AddBeforeMuls_StillProductThenSum()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Add, 4));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 1.5));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 2));

        Assert.Equal(34, sheet.Get(Stat.BeltSpeed));
        Assert.NotEqual(42, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_InterleavedAdd_StillProductThenSum()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 1.5));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Add, 4));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 2));

        Assert.Equal(34, sheet.Get(Stat.BeltSpeed));
        Assert.NotEqual(23, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_NoModifiers_ReturnsBase()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        Assert.Equal(10, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_AddsOnly_SumsOnBase()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Add, 3));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Add, 4));
        Assert.Equal(17, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_MulsOnly_ProductsOnBase()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 1.5));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 2));
        Assert.Equal(30, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_IgnoresOtherStats()
    {
        var sheet = new StatSheet();
        sheet.SetBase(Stat.BeltSpeed, 10);
        sheet.Add(new StatModifier(Stat.PlayerSpeed, StatOp.Mul, 2));
        sheet.Add(new StatModifier(Stat.PlayerSpeed, StatOp.Add, 5));
        Assert.Equal(10, sheet.Get(Stat.BeltSpeed));
    }

    [Fact]
    public void Get_UnsetBase_IsZeroTimesMulsPlusAdds()
    {
        var sheet = new StatSheet();
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Mul, 1.5));
        sheet.Add(new StatModifier(Stat.BeltSpeed, StatOp.Add, 4));
        Assert.Equal(4, sheet.Get(Stat.BeltSpeed));
    }
}
