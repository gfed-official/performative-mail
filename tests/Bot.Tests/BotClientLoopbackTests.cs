using PerformativeMail.BotClient;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Bot.Tests;

public sealed class BotClientLoopbackTests
{
    [Fact]
    public void BotClient_OverLoopback_DeliversOneLetter()
    {
        var world = BotWorld.CreateShift1World();
        var item = BotWorld.DepositShift1Letter(world);
        var loop = BotLoop.Connect(world);
        var walletBefore = loop.ReplicaWallet.Balance.Value;
        var walletSamples = new List<int>(900) { walletBefore };
        var last = PoseOf(loop);
        int movedTicks = 0;

        for (int i = 0; i < 900; i++)
        {
            if (loop.State.Phase == BotPhase.Stuck)
                break;
            loop.StepOnce();
            walletSamples.Add(loop.ReplicaWallet.Balance.Value);
            var now = PoseOf(loop);
            double stepCm = Math.Sqrt(
                (double)(now.Xcm - last.Xcm) * (now.Xcm - last.Xcm)
                + (double)(now.Ycm - last.Ycm) * (now.Ycm - last.Ycm));
            Assert.True(stepCm <= 25.0, $"Teleported {stepCm:0.0} cm in one tick.");
            if (stepCm > 0)
                movedTicks++;
            last = now;
            if (loop.Deliveries.Count > 0)
                break;
        }

        var walletAfter = loop.ReplicaWallet.Balance.Value;
        var paid = item.Value;
        Assert.True(
            loop.Deliveries.Count > 0,
            $"No delivery. phase={loop.State.Phase} tick={loop.Ticks} pos=({last.Xcm},{last.Ycm}) moved={movedTicks}");
        Assert.True(movedTicks >= 20, $"Bot did not walk. movedTicks={movedTicks}");
        Assert.True(loop.Ticks >= 12);
        Assert.Equal(1, CountDeliveries(loop, item.Id));
        Assert.True(walletAfter > walletBefore);
        Assert.Equal(walletBefore + paid, walletAfter);
        Assert.Equal(new Cents(paid), loop.Deliveries[0].Paid);
        for (int i = 1; i < walletSamples.Count; i++)
            Assert.True(walletSamples[i] >= walletSamples[i - 1], $"Wallet decreased at sample {i}.");
        Assert.False(ContainsMail(loop.Replica, loop.Intake, item.Id));
        Assert.False(ContainsMail(loop.Replica, loop.Hotbar, item.Id));
        Assert.NotNull(loop.Client.LastSnapshot);
        Assert.True(loop.Client.LocalPlayer.HasValue);
    }

    private static (int Xcm, int Ycm) PoseOf(BotLoop loop)
    {
        if (loop.Client.LastSnapshot is { } snapshot
            && loop.Client.LocalPlayer is { } self
            && OwnerSnapshot.TryFrom(snapshot, self, out var owner))
            return (owner.Pose.Xcm, owner.Pose.Ycm);

        var pose = loop.Client.Prediction.Pose;
        return (pose.Xcm, pose.Ycm);
    }

    private static int CountDeliveries(BotLoop loop, MailId mailId)
    {
        int n = 0;
        foreach (var delivery in loop.Deliveries)
        {
            if (delivery.MailId.Equals(mailId))
                n++;
        }

        return n;
    }

    private static bool ContainsMail(InventorySystem inventory, ContainerId container, MailId mailId)
    {
        if (!inventory.TryGetContainer(container, out var grid))
            return false;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is not MailStack mail)
                continue;
            foreach (var id in mail.Ids)
            {
                if (id.Equals(mailId))
                    return true;
            }
        }

        return false;
    }
}
