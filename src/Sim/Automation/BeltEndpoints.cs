using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;
using InventoryAccepted = PerformativeMail.Sim.Inventory.Accepted;

namespace PerformativeMail.Sim.Automation;

public readonly record struct WorldItem(int ItemId, MailKindId Kind, TileCoord Tile, uint DespawnTick);

public sealed class BeltEndpoints
{
    public const int WorldItemDespawnSeconds = 300;

    private readonly Dictionary<TileCoord, DestinationId> _mailboxes = new Dictionary<TileCoord, DestinationId>();
    private readonly Dictionary<TileCoord, ContainerId> _chests = new Dictionary<TileCoord, ContainerId>();
    private readonly List<WorldItem> _worldItems = new List<WorldItem>();
    private InventorySystem? _inventory;
    private MailRegistry? _mail;
    private ContainerId _intake;
    private TileCoord _intakeTile;
    private Facing _intakeFace;
    private bool _intakeBound;

    public IReadOnlyList<WorldItem> WorldItems => _worldItems;

    public void BindMailbox(TileCoord tile, DestinationId destination) => _mailboxes[tile] = destination;

    public void BindChest(TileCoord tile, ContainerId chest) => _chests[tile] = chest;

    public void BindIntake(
        ContainerId intake,
        TileCoord tile,
        Facing face,
        InventorySystem inventory,
        MailRegistry mail)
    {
        _intake = intake;
        _intakeTile = tile;
        _intakeFace = face;
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _mail = mail ?? throw new ArgumentNullException(nameof(mail));
        _intakeBound = true;
    }

    public void Drain(
        BeltNetwork belts,
        uint tick,
        Destinations dests,
        Wallet wallet,
        ComplaintMeter? complaint = null,
        byte shift = 1)
    {
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        if (dests is null) throw new ArgumentNullException(nameof(dests));
        if (wallet is null) throw new ArgumentNullException(nameof(wallet));

        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            if (segment.FeedsJunction) continue;
            var sink = Resolve(segment.AheadTile);
            for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
            {
                if (!segment.TryPeekHead(lane, out var item)) continue;
                if (!TryAccept(sink, item, tick, dests, wallet, complaint, shift)) continue;
                segment.TryTakeHead(lane, out _);
            }
        }
    }

    public void StepDespawn(uint tick)
    {
        for (int i = _worldItems.Count - 1; i >= 0; i--)
        {
            var item = _worldItems[i];
            if (tick < item.DespawnTick) continue;
            var id = new MailId(unchecked((uint)item.ItemId));
            if (_mail is null || !_mail.TryGet(id, out var mail))
            {
                _worldItems.RemoveAt(i);
                continue;
            }

            if (!_intakeBound || _inventory is null) continue;
            var stack = MailStack.Single(mail.Kind, mail.Address, mail.Id);
            if (_inventory.Apply(Actor.System, new Deposit(_intake, stack)) is InventoryAccepted)
                _worldItems.RemoveAt(i);
        }
    }

    public void Feed(BeltNetwork belts)
    {
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        if (!_intakeBound || _inventory is null) return;

        var start = BeltNetwork.Next(_intakeTile, _intakeFace);
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            if (segment.Tiles.Count == 0) continue;
            if (!segment.Tiles[0].Equals(start) || segment.Facing != _intakeFace)
                continue;
            if (!TryPopMinMail(out var mail))
                return;
            int itemId = (int)mail.Ids[0].Value;
            if (!segment.TryInsert(0, itemId, 0f, mail.Kind, mail.Address)
                && (mail.Kind.Equals(MailKinds.Cargo) || !segment.TryInsert(1, itemId, 0f, mail.Kind, mail.Address)))
                _inventory.Apply(Actor.System, new Deposit(_intake, mail));
            return;
        }
    }

    private BeltSink Resolve(TileCoord tile)
    {
        if (_mailboxes.TryGetValue(tile, out var destination))
            return new BeltSink(SinkKind.Mailbox, destination, default, tile);
        if (_chests.TryGetValue(tile, out var chest))
            return new BeltSink(SinkKind.Chest, default, chest, tile);
        return new BeltSink(SinkKind.Air, default, default, tile);
    }

    private bool TryAccept(
        in BeltSink sink,
        in BeltItem item,
        uint tick,
        Destinations dests,
        Wallet wallet,
        ComplaintMeter? complaint,
        byte shift)
    {
        switch (sink.Kind)
        {
            case SinkKind.Mailbox:
                var result = dests.TryDeliver(new MailId(unchecked((uint)item.ItemId)), sink.Destination, shift, wallet, complaint);
                return result is Delivered || result is Misdelivered;
            case SinkKind.Chest:
                return TryDepositChest(sink.Chest, item);
            case SinkKind.Air:
                uint despawn = tick + (uint)TickClock.TicksFromSeconds(WorldItemDespawnSeconds);
                _worldItems.Add(new WorldItem(item.ItemId, item.Kind, sink.Tile, despawn));
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(sink), sink.Kind, null);
        }
    }

    private bool TryDepositChest(ContainerId chest, in BeltItem item)
    {
        if (_inventory is null || _mail is null) return false;
        var id = new MailId(unchecked((uint)item.ItemId));
        if (!_mail.TryGet(id, out var mail)) return false;
        var stack = MailStack.Single(mail.Kind, mail.Address, mail.Id);
        return _inventory.Apply(Actor.System, new Deposit(chest, stack)) is InventoryAccepted;
    }

    private bool TryPopMinMail(out MailStack popped)
    {
        popped = null!;
        if (_inventory is null || !_inventory.TryGetContainer(_intake, out var grid))
            return false;

        MailId? min = null;
        EntryId hostId = default;
        foreach (var entry in grid.Entries)
        {
            if (entry.Stack is not MailStack mail) continue;
            for (int i = 0; i < mail.Ids.Count; i++)
            {
                var id = mail.Ids[i];
                if (min is null || id.Value < min.Value.Value)
                {
                    min = id;
                    hostId = entry.Id;
                }
            }
        }

        if (min is null) return false;

        if (_inventory.Apply(Actor.System, new Withdraw(_intake, hostId)) is not InventoryAccepted accepted
            || accepted.Withdrawn is not MailStack taken)
            return false;

        popped = MailStack.Single(taken.Kind, taken.Address, min.Value);
        if (taken.Count == 1) return true;

        var restIds = new MailId[taken.Count - 1];
        int w = 0;
        for (int i = 0; i < taken.Ids.Count; i++)
        {
            if (taken.Ids[i].Equals(min.Value)) continue;
            restIds[w++] = taken.Ids[i];
        }

        var rest = new MailStack(taken.Kind, taken.Address, restIds);
        if (_inventory.Apply(Actor.System, new Deposit(_intake, rest)) is InventoryAccepted)
            return true;

        _inventory.Apply(Actor.System, new Deposit(_intake, taken));
        popped = null!;
        return false;
    }

    private enum SinkKind : byte { Mailbox, Chest, Air }

    private readonly record struct BeltSink(SinkKind Kind, DestinationId Destination, ContainerId Chest, TileCoord Tile);
}
