using PerformativeMail.Sim.Balance;
using PerformativeMail.Sim.World;

var contentRoot = args.Length > 0 ? args[0] : "content";
var balancePath = Path.Combine(contentRoot, BalanceCatalog.RelativePath);

if (!File.Exists(balancePath))
{
    Console.Error.WriteLine($"Balance file not found. Path was {balancePath}");
    return 2;
}

BalanceTable balance;
try
{
    balance = BalanceCatalog.LoadFile(balancePath);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var shift1 = BalanceSim.RunHand(balance, 1);
var shift2 = BalanceSim.RunHand(balance, 2);
Console.WriteLine(BalanceSim.Line(in shift1));
Console.WriteLine(BalanceSim.Line(in shift2));
return BalanceSim.SoloHandShift1WinShift2Fail(balance) ? 0 : 1;
