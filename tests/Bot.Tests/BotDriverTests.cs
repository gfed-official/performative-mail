using PerformativeMail.BotClient;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Bot.Tests;

public sealed class BotDriverTests
{
    [Fact]
    public void Bot_DeliversOneLetter_WalletIncreases()
    {
        var atlas = LoadRepoAtlas();
        var world = new SimWorld(atlas, new BotTestCatalog(), seed: 1, jitterSeconds: 0);
        var player = world.Players.SpawnAtOrigin();
        var hotbar = world.Inventory!.CreateContainer(ContainerSpec.Hotbar, player.Id);
        var address = atlas.DeliverableAddresses[0];
        var mailId = world.Mail!.Allocate();
        var item = new MailItem(
            mailId,
            MailKinds.Letter,
            address,
            MailKinds.ValueAtSpawn(MailKinds.Letter, atlas.DistrictId, MailSpawnConstants.Shift1),
            MailSpawnConstants.Shift1,
            MailSpawnConstants.Shift1);
        Assert.True(world.Mail.Register(item));
        Assert.IsType<Accepted>(world.Inventory.Apply(
            Actor.System,
            new Deposit(world.Intake, MailStack.Single(item.Kind, item.Address, item.Id))));

        var driver = new BotDriver(world, player.Id, hotbar);
        var walletBefore = driver.Wallet.Balance.Value;
        var walletSamples = new List<int>(900) { walletBefore };

        for (int i = 0; i < 900; i++)
        {
            if (driver.State.Phase == BotPhase.Stuck)
                break;
            driver.StepOnce();
            walletSamples.Add(driver.Wallet.Balance.Value);
            if (driver.Deliveries.Count > 0)
                break;
        }

        var walletAfter = driver.Wallet.Balance.Value;
        var paid = item.Value;
        Assert.True(
            driver.Deliveries.Count > 0,
            $"No delivery. phase={driver.State.Phase} tick={world.CurrentTick} pos=({player.Xcm},{player.Ycm})");
        Assert.Equal(1, CountDeliveries(driver, mailId));
        Assert.True(walletAfter > walletBefore);
        Assert.Equal(walletBefore + paid, walletAfter);
        Assert.Equal(new Cents(paid), driver.Deliveries[0].Paid);
        for (int i = 1; i < walletSamples.Count; i++)
            Assert.True(walletSamples[i] >= walletSamples[i - 1], $"Wallet decreased at sample {i}.");
        Assert.False(ContainsMail(world.Inventory, world.Intake, mailId));
        Assert.False(ContainsMail(world.Inventory, hotbar, mailId));
    }

    private static int CountDeliveries(BotDriver driver, MailId mailId)
    {
        int n = 0;
        foreach (var delivery in driver.Deliveries)
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

    private static WorldAtlas LoadRepoAtlas()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "content", "world", "m0_test_map.json");
                if (File.Exists(candidate))
                    return WorldAtlasLoader.LoadFile(Path.GetFullPath(candidate));
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("content/world/m0_test_map.json");
    }
}

internal sealed class BotTestCatalog : IStackCatalog
{
    public Footprint FootprintOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (key.Def == MailKinds.Letter.Value || key.Def == MailKinds.Postcard.Value)
                return new Footprint(1, 1);
            if (key.Def == MailKinds.SmallPackage.Value)
                return new Footprint(1, 2);
            if (key.Def == MailKinds.MediumPackage.Value)
                return new Footprint(2, 2);
        }

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public int MaxStackOf(StackKey key)
    {
        if (key.IsMail)
        {
            if (key.Def == MailKinds.Letter.Value) return 20;
            if (key.Def == MailKinds.Postcard.Value) return 40;
            if (key.Def == MailKinds.SmallPackage.Value) return 1;
            if (key.Def == MailKinds.MediumPackage.Value) return 1;
        }

        throw new ArgumentException("Unknown stack key.", nameof(key));
    }

    public WeightClass WeightOf(StackKey key)
    {
        if (!key.IsMail)
            throw new ArgumentException("Unknown stack key.", nameof(key));
        return key.Def == MailKinds.MediumPackage.Value ? WeightClass.Medium : WeightClass.Light;
    }

    public StackCategory CategoryOf(StackKey key)
        => key.IsMail ? StackCategory.Mail : StackCategory.Material;
}
