using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.BotClient;

public readonly record struct BotCliOptions(int MaxSeconds, int UntilDeliveries)
{
    public int MaxTicks => MaxSeconds * TickClock.TickHz;
}

public static class BotCli
{
    public static int Run(string[] args)
    {
        if (!TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var world = BotWorld.CreateShift1World();
        BotWorld.DepositShift1Letter(world);
        var loop = BotLoop.Connect(world);
        var result = loop.Run(options.MaxTicks, options.UntilDeliveries);
        Console.WriteLine(result.Line);
        return result.ExitCode;
    }

    public static bool TryParse(string[] args, out BotCliOptions options, out string? error)
    {
        int maxSeconds = 60;
        int untilDeliveries = 1;
        options = default;
        error = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--max-seconds":
                    if (!TryReadInt(args, ref i, out maxSeconds) || maxSeconds < 1)
                    {
                        error = "Usage: --max-seconds <positive int> --until-deliveries <positive int>";
                        return false;
                    }

                    break;
                case "--until-deliveries":
                    if (!TryReadInt(args, ref i, out untilDeliveries) || untilDeliveries < 1)
                    {
                        error = "Usage: --max-seconds <positive int> --until-deliveries <positive int>";
                        return false;
                    }

                    break;
                default:
                    error = $"Unknown argument: {args[i]}";
                    return false;
            }
        }

        options = new BotCliOptions(maxSeconds, untilDeliveries);
        return true;
    }

    private static bool TryReadInt(string[] args, ref int index, out int value)
    {
        value = 0;
        if (index + 1 >= args.Length)
            return false;
        index++;
        return int.TryParse(args[index], out value);
    }
}
