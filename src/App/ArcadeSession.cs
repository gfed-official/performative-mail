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
        return Boot(WorldGen.GenerateSmallIsland(settings.Seed), settings);
    }

    public static ArcadeBoot CreateDebug()
    {
        return Boot(DebugWorld.Tables(), RunSettings.Arcade());
    }

    private static ArcadeBoot Boot(WorldTables tables, RunSettings settings)
    {
        ulong hash = WorldHash.Compute(tables);
        var atlas = WorldAtlas.FromTables(tables);
        var bundle = ContentBoot.Load(out _, out var catalog);
        var world = new SimWorld(atlas, catalog, unchecked((int)settings.Seed));
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

        var clock = new ShiftClock(bundle.Balance, RunState.InLobby());
        return new ArcadeBoot(
            world,
            new WorldOffer(settings.Seed, hash),
            settings,
            tables,
            bundle.Balance,
            destinations,
            clock);
    }
}
