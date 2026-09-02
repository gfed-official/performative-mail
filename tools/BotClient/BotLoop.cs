using System;
using System.Collections.Generic;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Server;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.World;

namespace PerformativeMail.BotClient;

public sealed record BotRunResult(
    int Delivered,
    int Misdelivered,
    int WalletCents,
    uint Ticks,
    int WalletBefore,
    bool Stuck)
{
    public bool WalletIncreased => WalletCents > WalletBefore;

    public int ExitCode => Delivered >= 1 && WalletIncreased ? 0 : 2;

    public string Line => $"delivered={Delivered} misdelivered={Misdelivered} wallet={WalletCents} ticks={Ticks}";
}

public sealed class BotLoop
{
    // spec/11-balance.md §8 interact range. spec/06 §4.3 3 m is server grace, not the walk target.
    public const float ReachMetres = 2.5f;

    private readonly SimWorld _world;
    private readonly EntityId _self;
    private readonly ContainerId _hotbar;
    private readonly Destinations _destinations;
    private readonly InventorySystem _replica;
    private readonly Wallet _serverWallet;
    private readonly BotTuning _tuning;
    private readonly List<BotDelivery> _deliveries = new();
    private readonly List<Mailbox> _mailboxes;
    private BotState _state;
    private int _interactTicks;
    private uint _viewTick;

    private BotLoop(
        ServerRuntime server,
        ClientRuntime client,
        SimWorld world,
        EntityId self,
        ContainerId hotbar,
        Destinations destinations,
        InventorySystem replica,
        List<Mailbox> mailboxes,
        BotTuning tuning)
    {
        Server = server;
        Client = client;
        _world = world;
        _self = self;
        _hotbar = hotbar;
        _destinations = destinations;
        _replica = replica;
        _mailboxes = mailboxes;
        _serverWallet = new Wallet();
        _tuning = tuning;
        ReplicaWallet = new Wallet();
    }

    public ServerRuntime Server { get; }

    public ClientRuntime Client { get; }

    public Wallet ReplicaWallet { get; }

    public InventorySystem Replica => _replica;

    public ContainerId Hotbar => _hotbar;

    public ContainerId Intake => _world.Intake;

    public BotState State => _state;

    public IReadOnlyList<BotDelivery> Deliveries => _deliveries;

    public int Misdelivered { get; private set; }

    public uint Ticks => _viewTick;

    public static BotLoop Connect(SimWorld world, BotTuning? tuning = null)
    {
        if (world is null) throw new ArgumentNullException(nameof(world));
        if (world.Atlas is null || world.Inventory is null || world.Mail is null)
            throw new ArgumentException("SimWorld must be constructed with an atlas, inventory, and mail registry.", nameof(world));

        var (server, client, _) = Boot.CreateListenHost(world);
        server.TickOnce();
        client.Receive();
        if (client.LocalPlayer is not EntityId self)
            throw new InvalidOperationException("Listen host did not assign LocalPlayer.");

        var hotbar = world.Inventory.CreateContainer(ContainerSpec.Hotbar, self);
        world.Inventory.Open(self, world.Intake);

        var replica = new InventorySystem(world.Inventory.Catalog);
        ApplyReplicaDelta(replica, world.Inventory.Snapshot(world.Intake));
        ApplyReplicaDelta(replica, world.Inventory.Snapshot(hotbar));

        var destinations = new Destinations(world.Mail);
        var mailboxes = new List<Mailbox>(world.Atlas.Houses.Count);
        foreach (var house in world.Atlas.Houses.Values)
        {
            var dest = DestinationOf(house.Address);
            if (!destinations.Register(new Destination(dest, DestinationType.HouseMailbox, house.Address)))
                throw new InvalidOperationException("Duplicate house destination.");
            var pose = house.Mailbox;
            mailboxes.Add(new Mailbox(MailboxEntity(house.Address), house.Address, FromCm(pose.XCm, pose.YCm)));
        }

        return new BotLoop(
            server,
            client,
            world,
            self,
            hotbar,
            destinations,
            replica,
            mailboxes,
            tuning ?? new BotTuning(ReachMetres));
    }

    public BotRunResult Run(int maxTicks, int untilDeliveries)
    {
        if (maxTicks < 1) throw new ArgumentOutOfRangeException(nameof(maxTicks));
        if (untilDeliveries < 1) throw new ArgumentOutOfRangeException(nameof(untilDeliveries));

        var walletBefore = ReplicaWallet.Balance.Value;
        for (int i = 0; i < maxTicks; i++)
        {
            if (_state.Phase == BotPhase.Stuck)
                break;
            if (_deliveries.Count >= untilDeliveries)
                break;
            StepOnce();
        }

        return new BotRunResult(
            _deliveries.Count,
            Misdelivered,
            ReplicaWallet.Balance.Value,
            _viewTick,
            walletBefore,
            _state.Phase == BotPhase.Stuck);
    }

    public void StepOnce()
    {
        var view = BuildView();
        var (command, next) = BotBrain.Step(in view, in _state, _tuning);
        _state = next;
        Apply(command, in view);
        _viewTick++;
    }

    private BotView BuildView()
    {
        MailStack? held = null;
        if (_replica.TryGetContainer(_hotbar, out var hotbar))
            held = FirstMail(hotbar);

        IntakeView? intake = null;
        if (_replica.TryGetContainer(_world.Intake, out var intakeGrid))
        {
            intake = new IntakeView(
                _world.Intake,
                TileCenter(_world.Atlas!.PostOffice.IntakeTile, _world.Atlas.TileCm),
                FirstMailEntry(intakeGrid));
        }

        return new BotView(
            _viewTick,
            _self,
            PoseFromClient(),
            held,
            _hotbar,
            intake,
            _mailboxes,
            ReplicaWallet.Balance.Value);
    }

    private BotPos PoseFromClient()
    {
        if (Client.LastSnapshot is { } snapshot && OwnerSnapshot.TryFrom(snapshot, _self, out var owner))
            return FromCm(owner.Pose.Xcm, owner.Pose.Ycm);

        var pose = Client.Prediction.Pose;
        return FromCm(pose.Xcm, pose.Ycm);
    }

    private void Apply(BotCommand command, in BotView view)
    {
        switch (command)
        {
            case BotCommand.Idle:
                _interactTicks = 0;
                Drive(0, 0, InputButtons.None);
                break;
            case BotCommand.Move move:
                _interactTicks = 0;
                Drive(ToAxis(move.DirX), ToAxis(move.DirY), InputButtons.None);
                break;
            case BotCommand.TakeFromIntake take:
                _interactTicks = 0;
                ApplyTake(take);
                Drive(0, 0, InputButtons.None);
                break;
            case BotCommand.Interact interact:
                Drive(0, 0, InputButtons.Interact);
                ApplyInteract(interact, in view);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unhandled BotCommand.");
        }
    }

    private void Drive(sbyte axisX, sbyte axisY, InputButtons buttons)
    {
        var cmd = new InputCmd(0, axisX, axisY, 0, buttons);
        Client.SubmitInput(in cmd);
        Client.TickOnce();
        Server.TickOnce();
        Client.Receive();
    }

    private void ApplyTake(BotCommand.TakeFromIntake take)
    {
        var result = _world.Inventory!.Apply(
            Actor.Player(_self),
            new QuickMove(take.Intake, take.Entry, _hotbar, Amount.Of(1)));
        if (result is not Accepted accepted)
            throw new InvalidOperationException("TakeFromIntake QuickMove was rejected.");
        ApplyReplicaDeltas(accepted);
    }

    private void ApplyInteract(BotCommand.Interact interact, in BotView view)
    {
        _interactTicks++;
        if (_interactTicks != _tuning.HoldTicks)
            return;
        if (view.Held is not { } held)
            return;
        if (!TryMailboxPos(interact.Dest, out var mailboxAt) || !InReach(view.At, mailboxAt))
            return;
        if (held.Ids.Count == 0)
            return;

        var mailId = held.Ids[0];
        var dest = new DestinationId(interact.Dest.Value);
        var result = _destinations.TryDeliver(mailId, dest, MailSpawnConstants.Shift1, _serverWallet);
        switch (result)
        {
            case Delivered delivered:
                ReplicaWallet.Credit(delivered.Paid);
                _deliveries.Add(new BotDelivery(mailId, delivered.Paid));
                WithdrawFromHotbar(mailId);
                break;
            case Misdelivered misdelivered:
                ReplicaWallet.TryDebit(misdelivered.Penalty);
                Misdelivered++;
                WithdrawFromHotbar(mailId);
                break;
            case Rejected:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, "Unhandled DeliverResult.");
        }
    }

    private void WithdrawFromHotbar(MailId mailId)
    {
        if (!_world.Inventory!.TryGetContainer(_hotbar, out var hotbar))
            return;
        if (!TryEntryWithMail(hotbar, mailId, out var entry))
            return;

        var result = _world.Inventory.Apply(Actor.Player(_self), new Withdraw(_hotbar, entry, Amount.Of(1)));
        if (result is Accepted accepted)
            ApplyReplicaDeltas(accepted);
    }

    private void ApplyReplicaDeltas(Accepted accepted)
    {
        foreach (var delta in accepted.Deltas)
            ApplyReplicaDelta(_replica, delta);
    }

    private static void ApplyReplicaDelta(InventorySystem replica, ContainerDelta delta)
    {
        var result = replica.ApplyDelta(delta);
        if (result != ReplicaResult.Applied)
            throw new InvalidOperationException($"Client replica rejected {delta.Container.Value}: {result}.");
    }

    private bool TryMailboxPos(EntityId dest, out BotPos at)
    {
        foreach (var mailbox in _mailboxes)
        {
            if (mailbox.Dest != dest)
                continue;
            at = mailbox.At;
            return true;
        }

        at = default;
        return false;
    }

    private bool InReach(BotPos from, BotPos to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        return MathF.Sqrt(dx * dx + dy * dy) <= _tuning.ReachMetres;
    }

    private static EntityId MailboxEntity(AddressId address) => new(address.Number);

    private static DestinationId DestinationOf(AddressId address) => new(address.Number);

    private static sbyte ToAxis(float dir)
    {
        var scaled = Math.Clamp(dir, -1f, 1f) * MovementStep.AxisFull;
        return (sbyte)Math.Clamp(Math.Round(scaled), sbyte.MinValue, sbyte.MaxValue);
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
