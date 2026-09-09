using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Server;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class DebugSessionTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);

    [Fact]
    public void MenuInspect_HasNoCheats()
    {
        using var machine = new PlaySessionMachine(new LoopbackStack());
        var snap = machine.Inspect();

        Assert.Equal(DebugConnection.Menu, snap.Connection);
        Assert.False(snap.Host);
        Assert.False(snap.CanCheat);
        Assert.False(machine.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.False(machine.TryAdvancePhase());
        Assert.False(machine.TryResetLocalPawn());
        Assert.False(machine.TryTeleportToIntake());
        Assert.False(machine.TryTeleportToMailbox());
        Assert.False(machine.TryGiveMail());
        Assert.False(machine.TryQuickMove(new ContainerId(1), new EntryId(1), new ContainerId(2)));
        Assert.False(machine.TryStockIntake());
        Assert.False(machine.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "axe")));
        Assert.False(machine.TryBuy("bandage_x3"));
        Assert.False(machine.TryShop(out _));
        Assert.False(machine.ShopPhaseOpen);
    }

    [Fact]
    public void HostInspect_ShowsLobbyTickSeedAndLocalPlayer()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var snap = host.Inspect();
        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.Equal(DebugConnection.Playing, snap.Connection);
        Assert.True(snap.Host);
        Assert.True(snap.CanCheat);
        Assert.Equal(play.LocalPlayer.Value, snap.LocalPlayer);
        Assert.Equal(RunPhase.Prep, snap.Phase);
        Assert.Equal((byte)1, snap.Shift);
        Assert.Equal(RunSettings.Arcade().Seed, snap.Seed);
        Assert.Equal(0x821670054873680EUL, snap.WorldHash);
        Assert.Equal(new Cents(0), snap.Wallet);
        Assert.True(snap.Tick.HasValue);
    }

    [Fact]
    public void HostGiveWallet_CreditsSimWorld()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.True(host.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.Equal(new Cents(1000), host.Inspect().Wallet);
        Assert.False(host.TryGiveWallet(new Cents(0)));
        Assert.Equal(new Cents(1000), host.Inspect().Wallet);
    }

    [Fact]
    public void HostPrep_ShopBuy_DebitsWalletThroughShopSession()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.True(host.ShopPhaseOpen);
        Assert.True(host.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.True(host.TryShop(out var before));
        Assert.Equal("PREP", before.PhaseLabel);
        Assert.Contains(before.Rows, row => row.Id == "bandage_x3" && row.PriceLabel == "$0.80" && row.CanBuy);
        Assert.Contains(before.Rows, row => row.Id == "axe" && row.PriceLabel == "$0.80");
        Assert.Contains(before.Rows, row => row.Id == "bp_pipes" && row.TagLabel == ShopFrame.UnlocksPrefix + "3");

        Assert.True(host.TryBuy("bandage_x3"));
        Assert.Equal(new Cents(920), host.Inspect().Wallet);
        Assert.True(host.TryShop(out var after));
        Assert.Equal("$9.20", after.WalletLabel);
        Assert.Equal(3, CountBagItems(host));
        Assert.False(host.TryBuy("bp_pipes"));
        Assert.Equal(new Cents(920), host.Inspect().Wallet);
    }

    [Fact]
    public void HostPrep_ShopBuy_Bike_SpawnsAndMounts()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.True(host.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.True(host.TryShop(out var shop));
        Assert.Contains(shop.Rows, row => row.Id == "bike" && row.CanBuy);

        Assert.True(host.TryBuy("bike"));
        Assert.True(host.TryHostWorld(out var world, out var local));
        Assert.Equal(1, world.Vehicles.Count);
        Assert.True(world.Players.TryGet(local, out var body));
        Assert.NotEqual(0u, body.VehicleId.Value);
        Assert.Equal(new Cents(880), host.Inspect().Wallet);

        Pump(host, ref now, MoveIntent.Idle, 4);
        var play = Assert.IsType<PlaySession.Playing>(host.State);
        var bike = Assert.Single(play.Vehicles);
        Assert.Equal(VehicleKind.Bike, bike.Kind);
        var pawn = Assert.Single(play.Pawns, p => p.Id == play.LocalPlayer);
        Assert.Equal(pawn.Pose, bike.Pose);
    }

    [Fact]
    public void HostDelivery_ShopBuy_Rejected()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);
        Assert.True(host.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.True(host.TryShop(out _));
        Assert.True(host.TryAdvancePhase());
        Assert.Equal(RunPhase.Delivery, host.Inspect().Phase);
        Assert.False(host.ShopPhaseOpen);
        Assert.False(host.TryBuy("bandage_x3"));
        Assert.Equal(new Cents(1000), host.Inspect().Wallet);
        Assert.Equal(0, CountBagItems(host));
    }

    [Fact]
    public void HostPayday_ShopBuy_Allowed()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);
        Assert.True(host.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.True(host.TryAdvancePhase());
        Assert.True(host.TryAdvancePhase());
        Assert.Equal(RunPhase.Payday, host.Inspect().Phase);
        Assert.True(host.ShopPhaseOpen);
        Assert.True(host.TryBuy("bandage_x3"));
        Assert.Equal(new Cents(920), host.Inspect().Wallet);
        Assert.Equal(3, CountBagItems(host));
    }

    [Fact]
    public void HostAdvancePhase_PrepToDelivery()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.Equal(RunPhase.Prep, host.Inspect().Phase);
        Assert.True(host.TryAdvancePhase());
        Assert.Equal(RunPhase.Delivery, host.Inspect().Phase);
    }

    [Fact]
    public void HostResetPawn_ReturnsToSpawn()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var before = LocalPose(host);
        var forward = new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None);
        Pump(host, ref now, forward, 30);
        Assert.NotEqual(before, LocalPose(host));

        Assert.True(host.TryResetLocalPawn());
        Pump(host, ref now, MoveIntent.Idle, 4);
        var play = Assert.IsType<PlaySession.Playing>(host.State);
        var spawn = SpawnRing.Pose(SpawnRing.CentreOf(WorldAtlas.FromTables(play.World!)), 0);
        Assert.Equal(spawn, LocalPose(host));
    }

    [Fact]
    public void GuestCheats_ReturnFalseAndDoNotTouchHostWallet()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        using var guest = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        guest.Join(stack.LocalTarget);
        PumpBoth(host, guest, ref now, MoveIntent.Idle, 8);

        var guestSnap = guest.Inspect();
        Assert.Equal(DebugConnection.Playing, guestSnap.Connection);
        Assert.False(guestSnap.Host);
        Assert.False(guestSnap.CanCheat);
        Assert.Equal(RunSettings.Arcade().Seed, guestSnap.Seed);
        Assert.Null(guestSnap.Wallet);

        Assert.False(guest.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents)));
        Assert.False(guest.TryAdvancePhase());
        Assert.False(guest.TryBuy("bandage_x3"));
        Assert.False(guest.TryShop(out _));
        Assert.False(guest.TryResetLocalPawn());
        Assert.False(guest.TryTeleportToIntake());
        Assert.False(guest.TryTeleportToMailbox());
        Assert.False(guest.TryGiveMail());
        Assert.False(guest.TryStockIntake());
        Assert.False(guest.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "axe")));
        Assert.False(guest.TrySpawn(new DebugSpawnId(DebugSpawnKind.Mail, "letter")));
        Assert.False(guest.TrySpawn(new DebugSpawnId(DebugSpawnKind.Bike, "bike")));
        Assert.Equal(new Cents(0), host.Inspect().Wallet);
        Assert.Equal(RunPhase.Prep, host.Inspect().Phase);
    }

    [Fact]
    public void HostTeleportToIntake_MovesLocalPawnToIntakeTile()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(play.World);
        Assert.NotEqual(IntakePose(play.World), LocalPose(host));

        Assert.True(host.TryTeleportToIntake());
        Pump(host, ref now, MoveIntent.Idle, 4);
        Assert.Equal(IntakePose(play.World), LocalPose(host));
    }

    [Fact]
    public void HostTeleportToMailbox_MovesLocalPawnToFirstMailbox()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(play.World);
        var mailbox = MailboxPose(play.World);
        Assert.NotEqual(mailbox, LocalPose(host));

        Assert.True(host.TryTeleportToMailbox());
        Pump(host, ref now, MoveIntent.Idle, 4);
        Assert.Equal(mailbox, LocalPose(host));
    }

    [Fact]
    public void HostGiveMail_DepositsStackWhenHotbarEmpty()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var before = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(before.Overlay);
        Assert.False(HasMail(before.Overlay.Value.Hotbar));

        Assert.True(host.TryGiveMail());
        Pump(host, ref now, MoveIntent.Idle, 4);
        var after = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(after.Overlay);
        Assert.True(HasMail(after.Overlay.Value.Hotbar));
        Assert.True(host.TryGiveMail());
    }

    [Fact]
    public void HostStockIntake_DepositsLetterAtIntake()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, MoveIntent.Idle, 8);

        var before = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(before.Overlay);
        Assert.False(HasMail(before.Overlay.Value.Hotbar));

        Assert.True(host.TryStockIntake());
        Pump(host, ref now, MoveIntent.Idle, 4);
        var after = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(after.Overlay);
        Assert.False(HasMail(after.Overlay.Value.Hotbar));
        Assert.Equal(new Cents(0), after.Hud.Wallet);
        Assert.True(host.TryGiveMail());
        Pump(host, ref now, MoveIntent.Idle, 4);
        after = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.True(HasMail(after.Overlay!.Value.Hotbar));
        Assert.Equal((byte)100, after.Hud.HpPct);
        Assert.Equal(1, after.Hud.WeightPoints);
        Assert.Equal("Wt 1", HudFrame.From(after.Hud).WeightLabel);
        Assert.True(host.TryStockIntake());
    }

    [Fact]
    public void HostDebug_StockIntakeTeleportInteract_CreditsWallet()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.HostDebug();
        Pump(host, ref now, MoveIntent.Idle, 8);

        Assert.True(host.TryStockIntake());
        Assert.True(host.TryTeleportToIntake());
        Pump(host, ref now, new MoveIntent(0, 0, 0, InputButtons.Interact), 4);

        var held = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(held.Overlay);
        Assert.True(HasMail(held.Overlay.Value.Hotbar));
        Assert.Equal(new Cents(0), held.Hud.Wallet);
        Assert.Equal((byte)100, held.Hud.HpPct);
        Assert.Equal(1, held.Hud.WeightPoints);

        var frame = OverlayFrame.From(held.Overlay.Value);
        OverlayCell cell = frame.Hotbar[1, 0];
        Assert.Equal("1", cell.CountLabel);
        Assert.Equal("1/1/1", cell.AddressLabel);
        Assert.Equal("1 1/1/1", cell.Text);
        Assert.Equal((byte)1, cell.District);
        Assert.False(cell.Pending);
        Assert.NotEqual("13", cell.AddressLabel);

        Assert.True(host.TryTeleportToMailbox());
        Pump(
            host,
            ref now,
            new MoveIntent(0, 0, 0, InputButtons.Interact),
            ServerRuntime.InteractHoldTicks + 4);

        var delivered = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(delivered.Overlay);
        Assert.False(HasMail(delivered.Overlay.Value.Hotbar));
        Assert.Equal(new Cents(MailKinds.LetterBaseValue), delivered.Hud.Wallet);
        Assert.Equal(0, delivered.Hud.WeightPoints);
    }

    private static PlayerPose LocalPose(PlaySessionMachine machine)
    {
        var play = Assert.IsType<PlaySession.Playing>(machine.State);
        return Assert.Single(play.Pawns, p => p.Role == PawnRole.Local).Pose;
    }

    private static PlayerPose IntakePose(WorldTables tables)
    {
        var tile = tables.PostOffice.IntakeTile;
        int half = tables.TileCm / 2;
        return new PlayerPose(tile.X * tables.TileCm + half, tile.Y * tables.TileCm + half, 0, 0);
    }

    private static PlayerPose MailboxPose(WorldTables tables)
    {
        var box = tables.Houses[0].Mailbox;
        return new PlayerPose(box.XCm, box.YCm, box.ZCm, 0);
    }

    private static bool HasMail(GridContainer container)
    {
        foreach (var entry in container.Entries)
        {
            if (entry.Stack is MailStack)
                return true;
        }

        return false;
    }

    private static int CountBagItems(PlaySessionMachine host)
    {
        Assert.True(host.TryHostWorld(out var world, out _));
        Assert.NotNull(world.Inventory);
        int n = 0;
        foreach (var container in world.Inventory.Containers)
        {
            if (container.Spec.Shape.Cols != 8 || container.Spec.Shape.Rows != 2)
                continue;
            foreach (var entry in container.Entries)
            {
                if (entry.Stack is ItemStack item)
                    n += item.Count;
            }
        }

        return n;
    }

    private static void Pump(PlaySessionMachine machine, ref TimeSpan now, in MoveIntent intent, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            machine.Pump(now, in intent);
        }
    }

    private static void PumpBoth(
        PlaySessionMachine host,
        PlaySessionMachine guest,
        ref TimeSpan now,
        in MoveIntent intent,
        int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            now += Tick;
            host.Pump(now, in intent);
            guest.Pump(now, in intent);
        }
    }
}
