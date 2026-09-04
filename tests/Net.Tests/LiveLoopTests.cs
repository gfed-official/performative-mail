using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Server;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Net.Tests;

public sealed class LiveLoopTests
{
    public const ulong GoldenWorldHash = 0x821670054873680EUL;

    [Fact]
    public void Host_OffersGoldenWorldHash_AndSpawnsAtPostOffice()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, 8);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.NotNull(play.World);
        Assert.Equal(GoldenWorldHash, WorldHash.Compute(play.World));
        Assert.Equal(GoldenWorldHash, host.Inspect().WorldHash);
        Assert.Equal(RunPhase.Prep, play.Hud.Phase);
        Assert.Equal((byte)1, play.Hud.Shift);
        Assert.Equal("PREP", HudFrame.From(play.Hud).PhaseLabel);
        Assert.NotEqual(PlayerPose.Origin, play.Pawns[0].Pose);
        Assert.Equal(SpawnRing.CentreOf(WorldAtlas.FromTables(play.World)), play.Pawns[0].Pose);
    }

    [Fact]
    public void ArcadeHello_OffersWorldHashBeforePrepJoin()
    {
        var boot = ArcadeSession.Create();
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), boot);
        var client = new ClientRuntime(MailStackCatalog.Default);
        client.Connect(loopback.B);
        server.TickOnce();
        client.Receive();

        Assert.Equal(GoldenWorldHash, boot.Offer.WorldHash);
        Assert.Equal(GoldenWorldHash, client.AcceptedWorldHash);
        Assert.NotNull(client.GeneratedWorld);
        Assert.Equal(RunPhase.Prep, server.Session.Phase);
        Assert.True(client.LocalPlayer.HasValue);
        Assert.NotNull(client.Inventory);
        Assert.True(LiveOverlay.TryFrom(client.Inventory, out var overlay));
        Assert.Equal(8, overlay.Hotbar.Spec.Shape.Cols);
        Assert.Equal(1, overlay.Hotbar.Spec.Shape.Rows);
        Assert.Equal(8, overlay.Inventory.Spec.Shape.Cols);
        Assert.Equal(2, overlay.Inventory.Spec.Shape.Rows);
    }

    [Fact]
    public void Clock_AdvancesPrepDeadline()
    {
        var boot = ArcadeSession.Create();
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), boot);
        var client = new ClientRuntime(MailStackCatalog.Default);
        client.Connect(loopback.B);
        server.TickOnce();
        client.Receive();

        uint deadline = server.Session.PhaseDeadlineTick;
        Assert.True(deadline > 0);
        uint start = server.Clock!.Now;
        for (int i = 0; i < 30; i++)
            server.TickOnce();

        Assert.Equal(RunPhase.Prep, server.Session.Phase);
        Assert.Equal(start + 30, server.Clock.Now);
        Assert.Equal(deadline - (start + 30), server.Session.RemainingTicks(server.Clock.Now));
    }

    [Fact]
    public void Interact_PickupAndDeliver_CreditsWallet()
    {
        var boot = ArcadeSession.Create();
        var house = boot.Tables.Houses[0];
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), boot);
        var client = new ClientRuntime(MailStackCatalog.Default);
        client.Connect(loopback.B);
        Handshake(server, client);
        Assert.True(server.TryAdvancePhase());
        Assert.Equal(RunPhase.Delivery, server.Session.Phase);

        var player = client.LocalPlayer!.Value;
        Assert.True(server.World.Players.TryGet(player, out var body));
        var mailId = server.World.Mail!.Allocate();
        var item = new MailItem(
            mailId,
            MailKinds.Letter,
            house.Address,
            MailKinds.LetterBaseValue,
            1,
            1);
        Assert.True(server.World.Mail.Register(item));
        var stack = MailStack.Single(MailKinds.Letter, house.Address, mailId);
        Assert.True(server.World.Inventory!.Apply(Actor.System, new Deposit(server.World.Intake, stack)) is Accepted);

        PlaceAtTile(body, boot.World.Atlas!.PostOffice.IntakeTile, boot.World.Atlas.TileCm);
        HoldInteract(server, client);
        Assert.True(HotbarHasMail(server, mailId));
        Assert.Equal(new Cents(0), server.World.Wallet.Balance);

        ReleaseInteract(server, client);
        body.SetPose(new PlayerPose(house.Mailbox.XCm, house.Mailbox.YCm, house.Mailbox.ZCm, 0));
        HoldInteract(server, client);
        Assert.False(HotbarHasMail(server, mailId));
        Assert.Equal(new Cents(MailKinds.LetterBaseValue), server.World.Wallet.Balance);
    }

    [Fact]
    public void Interact_FirstInRangeTick_PicksUpMail()
    {
        var boot = ArcadeSession.Create();
        var house = boot.Tables.Houses[0];
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), boot);
        var client = new ClientRuntime(MailStackCatalog.Default);
        client.Connect(loopback.B);
        Handshake(server, client);
        Assert.True(server.TryAdvancePhase());

        var player = client.LocalPlayer!.Value;
        Assert.True(server.World.Players.TryGet(player, out var body));
        var mailId = DepositLetter(server, house.Address);
        PlaceAtTile(body, boot.World.Atlas!.PostOffice.IntakeTile, boot.World.Atlas.TileCm);

        Assert.True(server.TryPickupAddress(player, out var incoming));
        Assert.False(string.IsNullOrEmpty(incoming));
        HoldInteract(server, client, 1);
        Assert.True(HotbarHasMail(server, mailId));
    }

    [Fact]
    public void Interact_HoldOutOfRange_DoesNotBurnPickup()
    {
        var boot = ArcadeSession.Create();
        var house = boot.Tables.Houses[0];
        var loopback = new LoopbackTransport();
        var server = new ServerRuntime(LoopbackLink.OverPipes(loopback.A), boot);
        var client = new ClientRuntime(MailStackCatalog.Default);
        client.Connect(loopback.B);
        Handshake(server, client);
        Assert.True(server.TryAdvancePhase());

        var player = client.LocalPlayer!.Value;
        Assert.True(server.World.Players.TryGet(player, out var body));
        DepositLetter(server, house.Address);

        HoldInteract(server, client, ServerRuntime.InteractHoldTicks);
        Assert.False(HotbarHasAnyMail(server));

        PlaceAtTile(body, boot.World.Atlas!.PostOffice.IntakeTile, boot.World.Atlas.TileCm);
        HoldInteract(server, client, 1);
        Assert.True(HotbarHasAnyMail(server));
    }

    [Fact]
    public void Host_ApproachIntakeWhileHoldingInteract_AcquiresMail()
    {
        var stack = new LoopbackStack();
        using var host = new PlaySessionMachine(stack);
        var now = TimeSpan.Zero;
        host.Host();
        Pump(host, ref now, 8);

        var play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.Equal(RunPhase.Prep, play.Hud.Phase);
        Assert.True(host.TryAdvancePhase());
        Pump(host, ref now, MailSpawnConstants.BatchIntervalTicks + MailSpawnConstants.BatchJitterSeconds * TickClock.TickHz + 30);

        play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.Equal(RunPhase.Delivery, play.Hud.Phase);
        Assert.NotNull(play.World);
        var local = Assert.Single(play.Pawns, p => p.Role == PawnRole.Local);
        WalkTowardIntake(host, play.World, ref now, local.Pose, InputButtons.Interact, 90);

        play = Assert.IsType<PlaySession.Playing>(host.State);
        Assert.IsType<InteractPrompt.Pickup>(play.Hud.Interact);
        Assert.True(HotbarHasMail(play.Overlay));
    }

    private static MailId DepositLetter(ServerRuntime server, AddressId address)
    {
        var mailId = server.World.Mail!.Allocate();
        var item = new MailItem(
            mailId,
            MailKinds.Letter,
            address,
            MailKinds.LetterBaseValue,
            1,
            1);
        Assert.True(server.World.Mail.Register(item));
        var stack = MailStack.Single(MailKinds.Letter, address, mailId);
        Assert.True(server.World.Inventory!.Apply(Actor.System, new Deposit(server.World.Intake, stack)) is Accepted);
        return mailId;
    }

    private static void Handshake(ServerRuntime server, ClientRuntime client)
    {
        for (int i = 0; i < 4; i++)
        {
            server.TickOnce();
            client.Receive();
        }

        Assert.True(client.LocalPlayer.HasValue);
    }

    private static void ReleaseInteract(ServerRuntime server, ClientRuntime client)
    {
        var cmd = new InputCmd(0, 0, 0, 0, InputButtons.None);
        client.SubmitInput(in cmd);
        client.SendInputs();
        server.TickOnce();
        client.Receive();
    }

    private static void HoldInteract(ServerRuntime server, ClientRuntime client, int ticks = ServerRuntime.InteractHoldTicks)
    {
        for (int i = 0; i < ticks; i++)
        {
            var cmd = new InputCmd(0, 0, 0, 0, InputButtons.Interact);
            client.SubmitInput(in cmd);
            client.SendInputs();
            server.TickOnce();
            client.Receive();
        }
    }

    private static bool HotbarHasAnyMail(ServerRuntime server)
    {
        foreach (var container in server.World.Inventory!.Containers)
        {
            if (container.Spec.Shape.Rows != 1 || container.Spec.Shape.Cols != 8)
                continue;
            foreach (var entry in container.Entries)
            {
                if (entry.Stack is MailStack)
                    return true;
            }
        }

        return false;
    }

    private static bool HotbarHasMail(ServerRuntime server, MailId mailId)
    {
        foreach (var container in server.World.Inventory!.Containers)
        {
            if (container.Spec.Shape.Rows != 1 || container.Spec.Shape.Cols != 8)
                continue;
            foreach (var entry in container.Entries)
            {
                if (entry.Stack is MailStack mail)
                {
                    for (int i = 0; i < mail.Ids.Count; i++)
                    {
                        if (mail.Ids[i].Equals(mailId))
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool HotbarHasMail(OverlayReplica? overlay)
    {
        if (overlay is not { } replica)
            return false;
        foreach (var entry in replica.Hotbar.Entries)
        {
            if (entry.Stack is MailStack)
                return true;
        }

        return false;
    }

    private static void WalkTowardIntake(
        PlaySessionMachine host,
        WorldTables world,
        ref TimeSpan now,
        PlayerPose start,
        InputButtons buttons,
        int maxSteps)
    {
        int half = world.TileCm / 2;
        int x = world.PostOffice.IntakeTile.X * world.TileCm + half;
        int y = world.PostOffice.IntakeTile.Y * world.TileCm + half;
        long reach = (long)WorldAtlasLoader.InteractRangeCm * WorldAtlasLoader.InteractRangeCm;
        var pose = start;
        var tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);
        for (int i = 0; i < maxSteps; i++)
        {
            long dist = DistSq(pose.Xcm, pose.Ycm, x, y);
            var intent = dist <= reach
                ? new MoveIntent(0, 0, pose.Yaw, buttons)
                : Toward(in pose, x, y, buttons);
            now += tick;
            host.Pump(now, in intent);
            var play = Assert.IsType<PlaySession.Playing>(host.State);
            pose = Assert.Single(play.Pawns, p => p.Role == PawnRole.Local).Pose;
            if (dist <= reach && HotbarHasMail(play.Overlay))
                return;
        }
    }

    private static MoveIntent Toward(in PlayerPose pose, int xcm, int ycm, InputButtons buttons)
    {
        int dx = xcm - pose.Xcm;
        int dy = ycm - pose.Ycm;
        double yawRad = Math.Atan2(dx, dy);
        var yaw = (ushort)((int)Math.Round(yawRad * 65536.0 / (Math.PI * 2.0), MidpointRounding.AwayFromZero) & 0xFFFF);
        return new MoveIntent(0, sbyte.MaxValue, yaw, buttons);
    }

    private static long DistSq(int ax, int ay, int bx, int by)
    {
        long dx = ax - bx;
        long dy = ay - by;
        return dx * dx + dy * dy;
    }

    private static void PlaceAtTile(PerformativeMail.Sim.Players.PlayerBody body, TileCoord tile, int tileCm)
    {
        int half = tileCm / 2;
        body.SetPose(new PlayerPose(tile.X * tileCm + half, tile.Y * tileCm + half, 0, 0));
    }

    private static void Pump(PlaySessionMachine host, ref TimeSpan now, int steps)
    {
        var tick = TimeSpan.FromSeconds(TickClock.TickDurationSeconds);
        for (int i = 0; i < steps; i++)
        {
            now += tick;
            host.Pump(now, MoveIntent.Idle);
        }
    }
}
