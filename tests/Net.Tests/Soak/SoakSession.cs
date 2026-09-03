using System.Diagnostics;
using System.Runtime;
using PerformativeMail.BotClient;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;
using MailRejected = PerformativeMail.Sim.Mail.Rejected;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class SoakSession
{
    private readonly ContainerId[] _hotbars;
    private readonly int[] _interactTicks;
    private readonly Destinations _destinations;
    private readonly Wallet[] _wallets;
    private readonly IReadOnlyList<Mailbox> _mailboxes;
    private readonly BotTuning _tuning;
    private readonly BotPos _intakeAt;

    private SoakSession(
        SoakConfig config,
        ServerRuntime server,
        LoopbackHub hub,
        SoakRoster roster,
        ContainerId[] hotbars,
        Destinations destinations,
        IReadOnlyList<Mailbox> mailboxes,
        BotTuning tuning,
        BotPos intakeAt)
    {
        Config = config;
        Server = server;
        Hub = hub;
        Roster = roster;
        Hashes = new HashTrace();
        Ticks = new TickLog();
        _hotbars = hotbars;
        _interactTicks = new int[SoakRoster.SeatCount];
        _destinations = destinations;
        _wallets = new Wallet[SoakRoster.SeatCount];
        for (int i = 0; i < _wallets.Length; i++)
            _wallets[i] = new Wallet();
        _mailboxes = mailboxes;
        _tuning = tuning;
        _intakeAt = intakeAt;
    }

    public SoakConfig Config { get; }

    public ServerRuntime Server { get; }

    public LoopbackHub Hub { get; }

    public SoakRoster Roster { get; }

    public HashTrace Hashes { get; }

    public TickLog Ticks { get; }

    public static SoakSession Start(SoakConfig config)
    {
        if (config is null)
            throw new ArgumentNullException(nameof(config));

        var world = BotWorld.CreateShift1World();
        var catalog = world.Inventory!.Catalog;
        var hub = LoopbackHub.ForSeats(SoakRoster.SeatCount);
        var server = new ServerRuntime(LoopbackLink.OverPipes(hub.ServerEnds), world);

        var seats = new SoakSeat[SoakRoster.SeatCount];
        for (int i = 0; i < seats.Length; i++)
        {
            var kind = i < SoakRoster.RealCount ? SeatKind.Real : SeatKind.Bot;
            var client = new ClientRuntime(catalog);
            client.Connect(hub.ClientEnds[i]);
            BotState? brain = kind == SeatKind.Bot
                ? new BotState(BotPhase.Idle, default, default)
                : null;
            seats[i] = new SoakSeat(new ConnectionId((byte)i), kind, client, hub.ClientEnds[i], brain);
        }

        var roster = SoakRoster.Create(seats);
        server.TickOnce();
        for (int i = 0; i < roster.Seats.Count; i++)
            roster.Seats[i].Client.Receive();

        var hotbars = new ContainerId[SoakRoster.SeatCount];
        for (int i = 0; i < roster.Seats.Count; i++)
        {
            var seat = roster.Seats[i];
            if (seat.Client.LocalPlayer is not EntityId player)
                throw new InvalidOperationException($"Seat {seat.Id.Value} did not complete Hello.");

            seat.Player = player;
            if (world.Inventory.Open(player, world.Intake) is not Accepted)
                throw new InvalidOperationException($"Seat {seat.Id.Value} could not open Intake.");

            hotbars[i] = world.Inventory.CreateContainer(ContainerSpec.Hotbar, player);
            if (world.Inventory.Open(player, hotbars[i]) is not Accepted)
                throw new InvalidOperationException($"Seat {seat.Id.Value} could not open hotbar.");
        }

        var destinations = new Destinations(world.Mail!);
        var mailboxes = new List<Mailbox>(world.Atlas!.Houses.Count);
        foreach (var house in world.Atlas.Houses.Values)
        {
            var dest = new DestinationId(house.Address.Number);
            if (!destinations.Register(new Destination(dest, DestinationType.HouseMailbox, house.Address)))
                throw new InvalidOperationException("Duplicate house destination.");

            var pose = house.Mailbox;
            mailboxes.Add(new Mailbox(
                new EntityId(house.Address.Number),
                house.Address,
                FromCm(pose.XCm, pose.YCm)));
        }

        var intakeAt = TileCenter(world.Atlas.PostOffice.IntakeTile, world.Atlas.TileCm);
        return new SoakSession(
            config,
            server,
            hub,
            roster,
            hotbars,
            destinations,
            mailboxes,
            new BotTuning(BotLoop.ReachMetres),
            intakeAt);
    }

    public SoakReport Run()
    {
        var mismatches = new List<HashWitness>();
        int connected = CountConnected();
        var watch = new Stopwatch();
        var thread = Thread.CurrentThread;
        var priority = thread.Priority;
        if (Config.PrimeTicks > 0)
            thread.Priority = ThreadPriority.Highest;

        try
        {
            Pump(Config.PrimeTicks, mismatches, watch, recordCpu: false, ref connected);
            if (Config.PrimeTicks > 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Pump(Config.DurationTicks, mismatches, watch, recordCpu: true, ref connected);
        }
        finally
        {
            thread.Priority = priority;
        }

        var witnesses = Hashes.Witnesses;
        bool sawVersion = false;
        for (int i = 0; i < witnesses.Count; i++)
        {
            if (witnesses[i].Version.Value > 0)
            {
                sawVersion = true;
                break;
            }
        }

        bool criterion1 = Config.DurationTicks == SoakDuration.Criterion1Ticks
            && connected == SoakRoster.SeatCount
            && mismatches.Count == 0
            && witnesses.Count > 0
            && sawVersion;

        var tickBudget = Ticks.Close(Config.WarmupTicks);
        return new SoakReport
        {
            TicksRun = Config.DurationTicks,
            ConnectedSeats = connected,
            Mismatches = mismatches,
            Witnesses = witnesses,
            TickBudget = tickBudget,
            Criterion1 = criterion1,
            Criterion5 = tickBudget.Pass,
        };
    }

    private void Pump(
        uint ticks,
        List<HashWitness> mismatches,
        Stopwatch watch,
        bool recordCpu,
        ref int connected)
    {
        for (uint tick = 0; tick < ticks; tick++)
        {
            DriveSeats();
            if (recordCpu)
            {
                var cpuMs = Config.PrimeTicks > 0
                    ? TimeTickOnce(watch)
                    : TimeTickOnceUnprotected(watch);
                Ticks.Add(new TickSample(Server.World.CurrentTick, cpuMs));
            }
            else
            {
                Server.TickOnce();
            }

            for (int i = 0; i < Roster.Seats.Count; i++)
                Roster.Seats[i].Client.Receive();

            int now = CountConnected();
            if (now < connected)
                connected = now;

            WitnessFlushed(mismatches);
        }
    }

    private double TimeTickOnceUnprotected(Stopwatch watch)
    {
        watch.Restart();
        Server.TickOnce();
        watch.Stop();
        return watch.Elapsed.TotalMilliseconds;
    }

    private double TimeTickOnce(Stopwatch watch)
    {
        if (!TryBeginNoGc())
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            TryBeginNoGc();
        }

        try
        {
            watch.Restart();
            Server.TickOnce();
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }
        finally
        {
            EndNoGc();
        }
    }

    private static bool TryBeginNoGc()
    {
        try
        {
            return GC.TryStartNoGCRegion(16 * 1024 * 1024);
        }
        catch (OutOfMemoryException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void EndNoGc()
    {
        try
        {
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
                GC.EndNoGCRegion();
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void DriveSeats()
    {
        for (int i = 0; i < Roster.Seats.Count; i++)
        {
            var seat = Roster.Seats[i];
            switch (seat.Kind)
            {
                case SeatKind.Real:
                    DrivePuppet(seat);
                    break;
                case SeatKind.Bot:
                    DriveBot(i, seat);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seat.Kind), seat.Kind, null);
            }
        }
    }

    private void DrivePuppet(SoakSeat seat)
    {
        var at = PoseOf(seat.Client);
        var cmd = Distance(at, _intakeAt) <= _tuning.ReachMetres
            ? new InputCmd(0, 0, 0, 0, InputButtons.None)
            : Toward(_intakeAt, at);
        Submit(seat, in cmd);
    }

    private void DriveBot(int index, SoakSeat seat)
    {
        if (seat.Brain is not BotState brain)
            throw new InvalidOperationException($"Bot seat {seat.Id.Value} is missing BotState.");

        var view = BuildView(index, seat);
        var (command, next) = BotBrain.Step(in view, in brain, _tuning);
        seat.Brain = next;
        ApplyBot(index, seat, command, in view);
    }

    private void ApplyBot(int index, SoakSeat seat, BotCommand command, in BotView view)
    {
        switch (command)
        {
            case BotCommand.Idle:
                _interactTicks[index] = 0;
                Submit(seat, new InputCmd(0, 0, 0, 0, InputButtons.None));
                break;
            case BotCommand.Move move:
                _interactTicks[index] = 0;
                Submit(seat, new InputCmd(0, ToAxis(move.DirX), ToAxis(move.DirY), 0, InputButtons.None));
                break;
            case BotCommand.TakeFromIntake take:
                _interactTicks[index] = 0;
                TakeFromIntake(seat, take);
                Submit(seat, new InputCmd(0, 0, 0, 0, InputButtons.None));
                break;
            case BotCommand.Interact interact:
                Submit(seat, new InputCmd(0, 0, 0, 0, InputButtons.Interact));
                TryDeliver(index, seat, interact, in view);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }

    private void TakeFromIntake(SoakSeat seat, BotCommand.TakeFromIntake take)
    {
        Server.World.Inventory!.Apply(
            Actor.Player(seat.Player),
            new QuickMove(take.Intake, take.Entry, HotbarOf(seat), Amount.Of(1)));
    }

    private void TryDeliver(int index, SoakSeat seat, BotCommand.Interact interact, in BotView view)
    {
        _interactTicks[index]++;
        if (_interactTicks[index] != _tuning.HoldTicks)
            return;
        if (view.Held is not { } held || held.Ids.Count == 0)
            return;
        if (!TryMailboxPos(interact.Dest, out var mailboxAt) || Distance(view.At, mailboxAt) > _tuning.ReachMetres)
            return;

        var mailId = held.Ids[0];
        var dest = new DestinationId(interact.Dest.Value);
        var result = _destinations.TryDeliver(mailId, dest, MailSpawnConstants.Shift1, _wallets[index]);
        switch (result)
        {
            case Delivered:
            case Misdelivered:
                WithdrawFromHotbar(seat, mailId);
                break;
            case MailRejected:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }

    private void WithdrawFromHotbar(SoakSeat seat, MailId mailId)
    {
        var hotbarId = HotbarOf(seat);
        if (!Server.World.Inventory!.TryGetContainer(hotbarId, out var hotbar))
            return;
        if (!TryEntryWithMail(hotbar, mailId, out var entry))
            return;

        Server.World.Inventory.Apply(Actor.Player(seat.Player), new Withdraw(hotbarId, entry, Amount.Of(1)));
    }

    private void Submit(SoakSeat seat, in InputCmd cmd)
    {
        seat.Client.SubmitInput(in cmd);
        seat.Client.SendInputs();
    }

    private void WitnessFlushed(List<HashWitness> mismatches)
    {
        var inventory = Server.World.Inventory!;
        var flushed = Server.LastFlushedDeltas;
        for (int i = 0; i < flushed.Count; i++)
        {
            var delta = flushed[i];
            var viewerHashes = new List<(ConnectionId Seat, ulong Hash)>();
            HashVerdict? firstFail = null;
            foreach (var viewer in inventory.ViewersOf(delta.Container))
            {
                if (!TrySeat(viewer, out var seat))
                {
                    firstFail ??= new HashVerdict.MissingViewer(new ConnectionId(byte.MaxValue));
                    continue;
                }

                if (seat.Client.Inventory is not InventorySystem replica
                    || !replica.TryGetContainer(delta.Container, out var grid))
                {
                    firstFail ??= new HashVerdict.VersionGap(seat.Id);
                    continue;
                }

                if (!grid.Version.Equals(delta.Version))
                    firstFail ??= new HashVerdict.VersionGap(seat.Id);

                viewerHashes.Add((seat.Id, grid.Hash));
            }

            var witness = new HashWitness(
                Server.World.CurrentTick,
                delta.Container,
                delta.Version,
                delta.Hash,
                viewerHashes);
            Hashes.Record(witness);

            var verdict = firstFail ?? Hashes.Check(witness);
            switch (verdict)
            {
                case HashVerdict.Match:
                    break;
                case HashVerdict.MissingViewer:
                case HashVerdict.HashMismatch:
                case HashVerdict.VersionGap:
                    mismatches.Add(witness);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null);
            }
        }
    }

    private BotView BuildView(int index, SoakSeat seat)
    {
        MailStack? held = null;
        var replica = seat.Client.Inventory;
        var hotbarId = _hotbars[index];
        if (replica is not null && replica.TryGetContainer(hotbarId, out var hotbar))
            held = FirstMail(hotbar);

        IntakeView? intake = null;
        var world = Server.World;
        if (replica is not null && replica.TryGetContainer(world.Intake, out var intakeGrid))
        {
            intake = new IntakeView(world.Intake, _intakeAt, FirstMailEntry(intakeGrid));
        }

        return new BotView(
            world.CurrentTick,
            seat.Player,
            PoseOf(seat.Client),
            held,
            hotbarId,
            intake,
            _mailboxes,
            _wallets[index].Balance.Value);
    }

    private bool TrySeat(EntityId player, out SoakSeat seat)
    {
        for (int i = 0; i < Roster.Seats.Count; i++)
        {
            if (!Roster.Seats[i].Player.Equals(player))
                continue;
            seat = Roster.Seats[i];
            return true;
        }

        seat = null!;
        return false;
    }

    private ContainerId HotbarOf(SoakSeat seat)
    {
        for (int i = 0; i < Roster.Seats.Count; i++)
        {
            if (ReferenceEquals(Roster.Seats[i], seat))
                return _hotbars[i];
        }

        throw new InvalidOperationException($"No hotbar for seat {seat.Id.Value}.");
    }

    private int CountConnected()
    {
        int n = 0;
        for (int i = 0; i < Roster.Seats.Count; i++)
        {
            var client = Roster.Seats[i].Client;
            if (client.Connection is not null && client.LocalPlayer.HasValue)
                n++;
        }

        return n;
    }

    private bool TryMailboxPos(EntityId dest, out BotPos at)
    {
        for (int i = 0; i < _mailboxes.Count; i++)
        {
            if (_mailboxes[i].Dest != dest)
                continue;
            at = _mailboxes[i].At;
            return true;
        }

        at = default;
        return false;
    }

    private static BotPos PoseOf(ClientRuntime client)
    {
        var pose = client.Prediction.Pose;
        return FromCm(pose.Xcm, pose.Ycm);
    }

    private static InputCmd Toward(BotPos to, BotPos from)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        return new InputCmd(0, ToAxis(dx / length), ToAxis(dy / length), 0, InputButtons.None);
    }

    private static sbyte ToAxis(float dir)
    {
        var scaled = Math.Clamp(dir, -1f, 1f) * MovementStep.AxisFull;
        return (sbyte)Math.Clamp(Math.Round(scaled), sbyte.MinValue, sbyte.MaxValue);
    }

    private static float Distance(BotPos a, BotPos b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static BotPos FromCm(int xcm, int ycm) => new(xcm / 100f, ycm / 100f);

    private static BotPos TileCenter(TileCoord tile, int tileCm)
        => new((tile.X + 0.5f) * tileCm / 100f, (tile.Y + 0.5f) * tileCm / 100f);

    private static MailStack? FirstMail(GridContainer container)
    {
        MailStack? found = null;
        var best = uint.MaxValue;
        foreach (var entry in container.Entries)
        {
            if (entry.Stack is not MailStack mail)
                continue;
            if (entry.Id.Value >= best)
                continue;
            best = entry.Id.Value;
            found = mail;
        }

        return found;
    }

    private static EntryId? FirstMailEntry(GridContainer container)
    {
        EntryId? found = null;
        var best = uint.MaxValue;
        foreach (var entry in container.Entries)
        {
            if (entry.Stack is not MailStack)
                continue;
            if (entry.Id.Value >= best)
                continue;
            best = entry.Id.Value;
            found = entry.Id;
        }

        return found;
    }

    private static bool TryEntryWithMail(GridContainer container, MailId mailId, out EntryId entryId)
    {
        foreach (var entry in container.Entries)
        {
            if (entry.Stack is not MailStack mail)
                continue;
            foreach (var id in mail.Ids)
            {
                if (!id.Equals(mailId))
                    continue;
                entryId = entry.Id;
                return true;
            }
        }

        entryId = default;
        return false;
    }
}
