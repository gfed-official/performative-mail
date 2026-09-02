using System;
using System.IO;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.BotClient;

public static class BotWorld
{
    public static WorldAtlas LoadRepoAtlas()
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

    public static SimWorld CreateShift1World()
        => new(LoadRepoAtlas(), BotCatalog.Default, seed: 1, jitterSeconds: 0);

    public static MailItem DepositShift1Letter(SimWorld world)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (world.Atlas is null || world.Inventory is null || world.Mail is null)
            throw new ArgumentException("SimWorld must be constructed with an atlas, inventory, and mail registry.", nameof(world));

        var address = world.Atlas.DeliverableAddresses[0];
        var mailId = world.Mail.Allocate();
        var item = new MailItem(
            mailId,
            MailKinds.Letter,
            address,
            MailKinds.ValueAtSpawn(MailKinds.Letter, world.Atlas.DistrictId, MailSpawnConstants.Shift1),
            MailSpawnConstants.Shift1,
            MailSpawnConstants.Shift1);
        if (!world.Mail.Register(item))
            throw new InvalidOperationException("Mail id was already registered.");
        if (world.Inventory.Apply(Actor.System, new Deposit(world.Intake, MailStack.Single(item.Kind, item.Address, item.Id))) is not Accepted)
            throw new InvalidOperationException("Intake deposit was rejected.");
        return item;
    }
}
