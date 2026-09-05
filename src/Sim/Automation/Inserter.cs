using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;
using InventoryAccepted = PerformativeMail.Sim.Inventory.Accepted;

namespace PerformativeMail.Sim.Automation;

public sealed class Inserter
{
    public const string BuildingId = "inserter";

    public static int TransferPeriodTicks => TickClock.TickHz * 4 / 5;

    private readonly List<BeltItem> _emitted = new List<BeltItem>();
    private MailKindId? _filter;
    private int _cooldown = TransferPeriodTicks;

    public Inserter(TileCoord tile, Facing facing)
    {
        Tile = tile;
        Facing = facing;
    }

    public TileCoord Tile { get; }

    public Facing Facing { get; }

    public TileCoord Behind => BeltNetwork.Next(Tile, Opposite(Facing));

    public TileCoord Ahead => BeltNetwork.Next(Tile, Facing);

    public MailKindId? Filter => _filter;

    public IReadOnlyList<BeltItem> Emitted => _emitted;

    public void SetFilter(MailKindId? kind) => _filter = kind;

    public bool TryReady(in BeltItem head)
    {
        if (_cooldown > 0)
        {
            _cooldown--;
            if (_cooldown > 0) return false;
        }

        return _filter is not MailKindId want || head.Kind.Equals(want);
    }

    public void Complete(in BeltItem item)
    {
        _emitted.Add(item);
        _cooldown = TransferPeriodTicks;
    }

    public void StepTicks(int ticks, in BeltItem head)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        for (int n = 0; n < ticks; n++)
        {
            if (!TryReady(head)) continue;
            Complete(head);
        }
    }

    private static Facing Opposite(Facing facing) => (Facing)(((int)facing + 2) & 3);
}

public sealed class InserterNetwork
{
    private readonly List<WiredInserter> _inserters = new List<WiredInserter>();
    private readonly Dictionary<TileCoord, ContainerId> _chests = new Dictionary<TileCoord, ContainerId>();
    private InventorySystem? _inventory;
    private MailRegistry? _mail;

    public IReadOnlyList<Inserter> Inserters
    {
        get
        {
            var rows = new Inserter[_inserters.Count];
            for (int i = 0; i < _inserters.Count; i++)
                rows[i] = _inserters[i].Machine;
            return rows;
        }
    }

    public void BindInventory(InventorySystem inventory, MailRegistry mail)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _mail = mail ?? throw new ArgumentNullException(nameof(mail));
    }

    public void BindChest(TileCoord tile, ContainerId chest) => _chests[tile] = chest;

    public void Compile(IReadOnlyList<ConstructRecord> constructs, BeltNetwork belts)
    {
        if (constructs is null) throw new ArgumentNullException(nameof(constructs));
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        _inserters.Clear();

        var rows = new List<ConstructRecord>();
        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (string.Equals(row.DefId, Inserter.BuildingId, StringComparison.Ordinal))
                rows.Add(row);
        }

        rows.Sort((a, b) =>
        {
            int byX = a.Tile.X.CompareTo(b.Tile.X);
            return byX != 0 ? byX : a.Tile.Y.CompareTo(b.Tile.Y);
        });

        for (int i = 0; i < rows.Count; i++)
            _inserters.Add(Wire(rows[i], belts));
    }

    public bool SetFilter(TileCoord tile, MailKindId? kind)
    {
        for (int i = 0; i < _inserters.Count; i++)
        {
            var wired = _inserters[i];
            if (!wired.Machine.Tile.Equals(tile)) continue;
            wired.Machine.SetFilter(kind);
            return true;
        }

        return false;
    }

    public void Step(BeltNetwork belts)
    {
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        for (int i = 0; i < _inserters.Count; i++)
            _inserters[i].Step(_chests, _inventory, _mail);
    }

    public void StepTicks(BeltNetwork belts, int ticks)
    {
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        float dt = (float)TickClock.TickDurationSeconds;
        for (int n = 0; n < ticks; n++)
        {
            belts.Step(dt);
            Step(belts);
        }
    }

    private static WiredInserter Wire(ConstructRecord row, BeltNetwork belts)
    {
        var machine = new Inserter(row.Tile, row.Rotation);
        var input = FindEnd(belts, machine.Tile);
        input?.MarkJunctionInput();
        return new WiredInserter(
            machine,
            input,
            FindStart(belts, machine.Ahead, machine.Facing));
    }

    private static BeltSegment? FindEnd(BeltNetwork belts, TileCoord ahead)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            if (segment.AheadTile.Equals(ahead))
                return segment;
        }

        return null;
    }

    private static BeltSegment? FindStart(BeltNetwork belts, TileCoord tile, Facing travel)
    {
        for (int i = 0; i < belts.Segments.Count; i++)
        {
            var segment = belts.Segments[i];
            if (segment.Tiles.Count == 0) continue;
            if (segment.Tiles[0].Equals(tile) && segment.Facing == travel)
                return segment;
        }

        return null;
    }

    private sealed class WiredInserter
    {
        private readonly BeltSegment? _input;
        private readonly BeltSegment? _output;
        private int _peekedLane = -1;
        private EntryId _peekedEntry;
        private MailId _peekedMail;
        private ContainerId _peekedChest;

        public WiredInserter(Inserter machine, BeltSegment? input, BeltSegment? output)
        {
            Machine = machine;
            _input = input;
            _output = output;
        }

        public Inserter Machine { get; }

        public void Step(
            Dictionary<TileCoord, ContainerId> chests,
            InventorySystem? inventory,
            MailRegistry? mail)
        {
            if (!TryPeek(chests, inventory, out var head)) return;
            if (!Machine.TryReady(head)) return;
            if (!TryTake(inventory, out var taken)) return;
            if (!TryEmit(chests, inventory, mail, taken))
            {
                Restore(inventory, taken);
                return;
            }

            Machine.Complete(taken);
        }

        private bool TryPeek(
            Dictionary<TileCoord, ContainerId> chests,
            InventorySystem? inventory,
            out BeltItem item)
        {
            _peekedLane = -1;
            _peekedEntry = default;
            _peekedMail = default;
            _peekedChest = default;
            if (chests.ContainsKey(Machine.Behind))
                return TryPeekChest(chests, inventory, out item);
            return TryPeekBelt(out item);
        }

        private bool TryPeekChest(
            Dictionary<TileCoord, ContainerId> chests,
            InventorySystem? inventory,
            out BeltItem item)
        {
            item = default;
            if (inventory is null) return false;
            if (!chests.TryGetValue(Machine.Behind, out var chest)) return false;
            if (!inventory.TryGetContainer(chest, out var grid)) return false;

            foreach (var entry in grid.Entries)
            {
                if (entry.Stack is not MailStack stack) continue;
                var id = stack.Ids[0];
                item = new BeltItem((int)id.Value, 0f, stack.Kind, stack.Address);
                _peekedChest = chest;
                _peekedEntry = entry.Id;
                _peekedMail = id;
                return true;
            }

            return false;
        }

        private bool TryPeekBelt(out BeltItem item)
        {
            item = default;
            if (_input is null) return false;
            for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
            {
                if (!_input.TryPeekHead(lane, out item)) continue;
                _peekedLane = lane;
                return true;
            }

            return false;
        }

        private bool TryTake(InventorySystem? inventory, out BeltItem item)
        {
            item = default;
            if (_peekedLane >= 0)
            {
                if (_input is null) return false;
                return _input.TryTakeHead(_peekedLane, out item);
            }

            return TryTakeChest(inventory, out item);
        }

        private void Restore(InventorySystem? inventory, in BeltItem item)
        {
            if (_peekedLane >= 0)
            {
                if (_input is null) return;
                _input.TryInsert(
                    _peekedLane,
                    item.ItemId,
                    _input.LengthMetres,
                    item.Kind,
                    item.Address);
                return;
            }

            if (inventory is null) return;
            inventory.Apply(
                Actor.System,
                new Deposit(_peekedChest, MailStack.Single(item.Kind, item.Address, _peekedMail)));
        }

        private bool TryTakeChest(InventorySystem? inventory, out BeltItem item)
        {
            item = default;
            if (inventory is null) return false;
            if (inventory.Apply(Actor.System, new Withdraw(_peekedChest, _peekedEntry)) is not InventoryAccepted accepted
                || accepted.Withdrawn is not MailStack taken)
                return false;

            item = new BeltItem((int)_peekedMail.Value, 0f, taken.Kind, taken.Address);
            if (taken.Count == 1) return true;

            var restIds = new MailId[taken.Count - 1];
            int w = 0;
            for (int i = 0; i < taken.Ids.Count; i++)
            {
                if (taken.Ids[i].Equals(_peekedMail)) continue;
                restIds[w++] = taken.Ids[i];
            }

            var rest = new MailStack(taken.Kind, taken.Address, restIds);
            if (inventory.Apply(Actor.System, new Deposit(_peekedChest, rest)) is InventoryAccepted)
                return true;

            inventory.Apply(Actor.System, new Deposit(_peekedChest, taken));
            item = default;
            return false;
        }

        private bool TryEmit(
            Dictionary<TileCoord, ContainerId> chests,
            InventorySystem? inventory,
            MailRegistry? mail,
            in BeltItem item)
        {
            if (chests.TryGetValue(Machine.Ahead, out var chest))
                return TryDepositChest(inventory, mail, chest, item);
            if (_output is null) return false;
            if (_output.TryInsert(0, item.ItemId, 0f, item.Kind, item.Address))
                return true;
            return !item.Kind.Equals(MailKinds.Cargo)
                && _output.TryInsert(1, item.ItemId, 0f, item.Kind, item.Address);
        }

        private static bool TryDepositChest(
            InventorySystem? inventory,
            MailRegistry? mail,
            ContainerId chest,
            in BeltItem item)
        {
            if (inventory is null || mail is null) return false;
            var id = new MailId(unchecked((uint)item.ItemId));
            if (!mail.TryGet(id, out var row)) return false;
            var stack = MailStack.Single(row.Kind, row.Address, row.Id);
            return inventory.Apply(Actor.System, new Deposit(chest, stack)) is InventoryAccepted;
        }
    }
}
