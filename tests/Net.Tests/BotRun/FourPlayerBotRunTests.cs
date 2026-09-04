using PerformativeMail.BotClient;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim.Balance;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.BotRun;

public sealed class FourPlayerBotRunTests
{
    [Fact]
    public void FourPlayerFiveShift_PaydaysDurationAndHashes()
    {
        var balance = BalanceCatalog.LoadFile(Path.Combine(FindContentRoot(), BalanceCatalog.RelativePath));
        var run = FiveShiftRun.Drive(balance);
        int mismatches = PulseFourClientHashes();

        Assert.Equal(5, run.PaydayCount);
        Assert.Equal(1450, run.DurationSeconds);
        Assert.True(run.DurationSeconds < BalanceSim.MaxRunSeconds);
        Assert.True(run.GateHolds);
        Assert.Equal(0, mismatches);
        Assert.True(BalanceSim.SoloHandShift1WinShift2Fail(balance));

        for (byte shift = 1; shift <= 5; shift++)
        {
            var payday = run.Payday(shift);
            Console.WriteLine(BalanceSim.PaydayLine(in payday));
        }

        Console.WriteLine(BalanceSim.DurationLine(run.DurationSeconds));
        Console.WriteLine(BalanceSim.HashesLine(mismatches));
    }

    [Fact]
    public void FourClientLoopback_IntakeDeposit_HashesMatch()
    {
        Assert.Equal(0, PulseFourClientHashes());
    }

    private static int PulseFourClientHashes()
    {
        var world = BotWorld.CreateShift1World();
        var catalog = world.Inventory!.Catalog;
        var hub = LoopbackHub.ForSeats(BalanceSim.FourPlayerCount);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);
        var clients = new ClientRuntime[BalanceSim.FourPlayerCount];
        for (int i = 0; i < clients.Length; i++)
        {
            clients[i] = new ClientRuntime(catalog);
            clients[i].Connect(hub.ClientEnds[i]);
        }

        server.TickOnce();
        ReceiveAll(clients);
        Assert.Equal(BalanceSim.FourPlayerCount, server.JoinedCount);

        for (int i = 0; i < clients.Length; i++)
        {
            if (clients[i].LocalPlayer is not EntityId player)
                throw new InvalidOperationException($"Seat {i} did not complete Hello.");
            if (world.Inventory.Open(player, world.Intake) is not Accepted)
                throw new InvalidOperationException($"Seat {i} could not open Intake.");
        }

        BotWorld.DepositShift1Letter(world);
        server.TickOnce();
        ReceiveAll(clients);

        if (!world.Inventory.TryGetContainer(world.Intake, out var intake))
            throw new InvalidOperationException("Intake missing on server.");

        int mismatches = 0;
        for (int i = 0; i < clients.Length; i++)
        {
            var replica = clients[i].Inventory;
            if (replica is null || !replica.TryGetContainer(world.Intake, out var grid))
            {
                mismatches++;
                continue;
            }

            if (grid.Hash != intake.Hash || !grid.Version.Equals(intake.Version))
                mismatches++;
        }

        return mismatches;
    }

    private static void ReceiveAll(ClientRuntime[] clients)
    {
        for (int i = 0; i < clients.Length; i++)
            clients[i].Receive();
    }

    private static string FindContentRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content");
                if (File.Exists(Path.Combine(candidate, BalanceCatalog.RelativePath)))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/balance.json");
    }
}
