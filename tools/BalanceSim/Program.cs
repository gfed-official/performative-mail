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

var run = FiveShiftRun.Drive(balance);
for (byte shift = 1; shift <= 5; shift++)
{
    var payday = run.Payday(shift);
    Console.WriteLine(BalanceSim.PaydayLine(in payday));
}

Console.WriteLine(BalanceSim.DurationLine(run.DurationSeconds));
return BalanceSim.SoloHandShift1WinShift2Fail(balance) && run.GateHolds ? 0 : 1;
