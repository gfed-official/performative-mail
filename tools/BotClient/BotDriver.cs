using System;
using System.Collections.Generic;
using PerformativeMail.Sim;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.BotClient;

public readonly record struct BotDelivery(MailId MailId, Cents Paid);

public sealed class BotDriver
{
    // spec/11-balance.md §8 interact range. spec/06 §4.3 3 m is server grace, not the walk target.
    public const float ReachMetres = 2.5f;

    private readonly SimWorld _world;
    private readonly EntityId _self;
    private readonly ContainerId _hotbar;
    private readonly Destinations _destinations;
    private readonly BotTuning _tuning;
    private readonly List<BotDelivery> _deliveries = new();
    private BotState _state;
    private int _interactTicks;

    public BotDriver(SimWorld world, EntityId self, ContainerId hotbar, Wallet? wallet = null, BotTuning? tuning = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        if (world.Atlas is null || world.Inventory is null || world.Mail is null)
            throw new ArgumentException("SimWorld must be constructed with an atlas, inventory, and mail registry.", nameof(world));

        _self = self;
        _hotbar = hotbar;
        Wallet = wallet ?? new Wallet();
        _tuning = tuning ?? new BotTuning(ReachMetres);
        _destinations = new Destinations(world.Mail);
        foreach (var house in world.Atlas.Houses.Values)
        {
            var dest = DestinationOf(house.Address);
            if (!_destinations.Register(new Destination(dest, DestinationType.HouseMailbox, house.Address)))
                throw new InvalidOperationException("Duplicate house destination.");
        }

        world.Inventory.Open(self, world.Intake);
    }

    public Wallet Wallet { get; }

    public BotState State => _state;

    public IReadOnlyList<BotDelivery> Deliveries => _deliveries;

    public void StepOnce()
    {
        var view = BuildView();
        var (command, next) = BotBrain.Step(in view, in _state, _tuning);
        _state = next;
        Apply(command, in view);
        _world.Tick(_world.CurrentTick + 1);
    }

    public static EntityId MailboxEntity(AddressId address) => new(address.Number);

    public static DestinationId DestinationOf(AddressId address) => new(address.Number);

    private BotView BuildView()
    {
        if (!_world.Players.TryGet(_self, out var body))
            throw new InvalidOperationException("Player is not in SimWorld.");

        var inventory = _world.Inventory!;
        var atlas = _world.Atlas!;
        MailStack? held = null;
        if (inventory.TryGetContainer(_hotbar, out var hotbar))
            held = FirstMail(hotbar);

        IntakeView? intake = null;
        if (inventory.TryGetContainer(_world.Intake, out var intakeGrid))
        {
            intake = new IntakeView(
                _world.Intake,
                TileCenter(atlas.PostOffice.IntakeTile, atlas.TileCm),
                FirstMailEntry(intakeGrid));
        }

        return new BotView(
            _world.CurrentTick,
            _self,
            FromCm(body.Xcm, body.Ycm),
            held,
            _hotbar,
            intake,
            BuildMailboxes(atlas),
            Wallet.Balance.Value);
    }

    private static IReadOnlyList<Mailbox> BuildMailboxes(WorldAtlas atlas)
    {
        var boxes = new Mailbox[atlas.Houses.Count];
        int i = 0;
        foreach (var house in atlas.Houses.Values)
        {
            var pose = house.Mailbox;
            boxes[i++] = new Mailbox(MailboxEntity(house.Address), house.Address, FromCm(pose.XCm, pose.YCm));
        }

        return boxes;
    }

    private void Apply(BotCommand command, in BotView view)
    {
        switch (command)
        {
            case BotCommand.Idle:
                _interactTicks = 0;
                break;
            case BotCommand.Move move:
                _interactTicks = 0;
                ApplyMove(move);
                break;
            case BotCommand.TakeFromIntake take:
                _interactTicks = 0;
                ApplyTake(take);
                break;
            case BotCommand.Interact interact:
                ApplyInteract(interact, in view);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unhandled BotCommand.");
        }
    }

    private void ApplyMove(BotCommand.Move move)
    {
        var cmd = new InputCmd(
            _world.CurrentTick,
            ToAxis(move.DirX),
            ToAxis(move.DirY),
            0,
            InputButtons.None);
        _world.ApplyInput(_self, in cmd);
    }

    private void ApplyTake(BotCommand.TakeFromIntake take)
    {
        var result = _world.Inventory!.Apply(
            Actor.Player(_self),
            new QuickMove(take.Intake, take.Entry, _hotbar, Amount.Of(1)));
        if (result is not Accepted)
            throw new InvalidOperationException("TakeFromIntake QuickMove was rejected.");
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
        var result = _destinations.TryDeliver(mailId, dest, MailSpawnConstants.Shift1, Wallet);
        if (result is not Delivered delivered)
            return;

        _deliveries.Add(new BotDelivery(mailId, delivered.Paid));
        WithdrawFromHotbar(mailId);
    }

    private void WithdrawFromHotbar(MailId mailId)
    {
        if (!_world.Inventory!.TryGetContainer(_hotbar, out var hotbar))
            return;
        if (!TryEntryWithMail(hotbar, mailId, out var entry))
            return;

        _world.Inventory.Apply(Actor.Player(_self), new Withdraw(_hotbar, entry, Amount.Of(1)));
    }

    private bool TryMailboxPos(EntityId dest, out BotPos at)
    {
        foreach (var house in _world.Atlas!.Houses.Values)
        {
            if (MailboxEntity(house.Address) != dest)
                continue;
            at = FromCm(house.Mailbox.XCm, house.Mailbox.YCm);
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
