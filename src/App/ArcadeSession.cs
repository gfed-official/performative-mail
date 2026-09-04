using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public static class ArcadeSession
{
    public static ArcadeBoot Create()
    {
        var settings = RunSettings.Arcade();
        var tables = WorldGen.GenerateSmallIsland(settings.Seed);
        ulong hash = WorldHash.Compute(tables);
        var atlas = WorldAtlas.FromTables(tables);
        var world = new SimWorld(atlas, MailStackCatalog.Default, unchecked((int)settings.Seed));
        if (world.Mail is null)
            throw new InvalidOperationException("Arcade world has no mail registry.");

        var destinations = new Destinations(world.Mail);
        for (int i = 0; i < tables.Houses.Length; i++)
        {
            var house = tables.Houses[i];
            destinations.Register(new Destination(
                new DestinationId(house.Address.Packed),
                DestinationType.HouseMailbox,
                house.Address));
        }

        string root = ContentRoot.Find();
        var balance = BalanceCatalog.LoadFile(Path.Combine(root, BalanceCatalog.RelativePath));
        var clock = new ShiftClock(balance, RunState.InLobby());
        return new ArcadeBoot(
            world,
            new WorldOffer(settings.Seed, hash),
            settings,
            tables,
            balance,
            destinations,
            clock);
    }
}
