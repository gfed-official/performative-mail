using PerformativeMail.App;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests.App;

public sealed class DebugSpawnTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);

    [Fact]
    public void HostTrySpawn_Item_DepositsIntoBag()
    {
        using var host = Play(out var now);
        ContentBoot.Load(out var ids, out _);
        Assert.True(ids.TryItem("axe", out var axe));

        Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "axe")));
        Pump(host, ref now, 4);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(play.Overlay);
        Assert.True(HasItem(play.Overlay.Value.Inventory, axe));
    }

    [Fact]
    public void HostTrySpawn_Mail_DepositsLetter()
    {
        using var host = Play(out var now);

        Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Mail, "letter")));
        Pump(host, ref now, 4);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(play.Overlay);
        Assert.True(HasMail(play.Overlay.Value.Hotbar, MailKinds.Letter) ||
                    HasMail(play.Overlay.Value.External, MailKinds.Letter));
    }

    [Fact]
    public void HostTrySpawn_Bike_SpawnsAndMounts()
    {
        using var host = Play(out _);

        Assert.True(host.TrySpawn(new DebugSpawnId(DebugSpawnKind.Bike, "bike")));
        Assert.True(host.TryHostWorld(out var world, out var local));
        Assert.Equal(1, world.Vehicles.Count);
        Assert.True(world.Players.TryGet(local, out var body));
        Assert.NotEqual(0u, body.VehicleId.Value);
    }

    [Fact]
    public void GuestTrySpawn_ReturnsFalse()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, 8);

        Assert.False(guest.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "axe")));
        Assert.False(guest.TrySpawn(new DebugSpawnId(DebugSpawnKind.Mail, "letter")));
        Assert.False(guest.TrySpawn(new DebugSpawnId(DebugSpawnKind.Bike, "bike")));
        Assert.True(host.TryHostWorld(out var world, out _));
        Assert.Equal(0, world.Vehicles.Count);
    }

    [Fact]
    public void SpawnCatalog_MatchesContentBundle()
    {
        using var host = new PlaySessionMachine(new LoopbackStack());
        var bundle = ContentFiles.Load(ContentRoot.Find());
        DebugSpawnCoverage.RequireComplete(bundle, host.SpawnCatalog);
    }

    private static PlaySessionMachine Play(out TimeSpan now)
    {
        var host = new PlaySessionMachine(new LoopbackStack());
        now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, 8);
        return host;
    }

    private static bool HasItem(GridContainer container, ItemDefId item)
    {
        foreach (var entry in container.Entries)
        {
            if (entry.Stack is ItemStack stack && stack.Item.Equals(item))
                return true;
        }

        return false;
    }

    private static bool HasMail(GridContainer? container, MailKindId kind)
    {
        if (container is null)
            return false;
        foreach (var entry in container.Entries)
        {
            if (entry.Stack is MailStack mail && mail.Kind.Equals(kind))
                return true;
        }

        return false;
    }

    private static void Pump(PlaySessionMachine machine, ref TimeSpan now, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            machine.Pump(now, MoveIntent.Idle);
        }
    }

    private static void PumpBoth(PlaySessionMachine host, PlaySessionMachine guest, ref TimeSpan now, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            host.Pump(now, MoveIntent.Idle);
            guest.Pump(now, MoveIntent.Idle);
        }
    }
}
