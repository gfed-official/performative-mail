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

    private static void HoldInteract(ServerRuntime server, ClientRuntime client)
    {
        for (int i = 0; i < ServerRuntime.InteractHoldTicks; i++)
        {
            var cmd = new InputCmd(0, 0, 0, 0, InputButtons.Interact);
            client.SubmitInput(in cmd);
            client.SendInputs();
            server.TickOnce();
            client.Receive();
        }
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
