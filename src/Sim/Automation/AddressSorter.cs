using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Automation;

public enum SorterOutput : byte
{
    Left = 0,
    Forward = 1,
    Right = 2,
    Overflow = 3
}

public readonly record struct AddressFilter(
    byte? District = null,
    byte? Street = null,
    byte? NumberMin = null,
    byte? NumberMax = null,
    MailKindId? Kind = null,
    byte? Unit = null)
{
    public bool HasConstraint =>
        District.HasValue
        || Street.HasValue
        || NumberMin.HasValue
        || NumberMax.HasValue
        || Kind.HasValue
        || Unit.HasValue;

    public bool Matches(in BeltItem item)
    {
        if (!HasConstraint) return false;
        var address = item.Address;
        if (District is byte district && address.District != district) return false;
        if (Street is byte street && address.Street != street) return false;
        if (NumberMin is byte min && address.Number < min) return false;
        if (NumberMax is byte max && address.Number > max) return false;
        if (Kind is MailKindId kind && !item.Kind.Equals(kind)) return false;
        if (Unit is byte unit && address.Unit != unit) return false;
        return true;
    }

    public static AddressFilter ForStreet(byte street) => new(Street: street);

    public static AddressFilter ForDistrict(byte district) => new(District: district);

    public static AddressFilter ForKind(MailKindId kind) => new(Kind: kind);

    public static AddressFilter ForUnit(byte unit) => new(Unit: unit);

    public static AddressFilter ForNumberRange(byte min, byte max) => new(NumberMin: min, NumberMax: max);
}

public sealed class AddressSorter
{
    public const string BuildingId = "address_sorter_mk1";
    public const int BufferSlots = 8;
    public const int FilterSlotsPerOutput = 1;
    public const int FilteredOutputs = 3;

    public static int ExaminePeriodTicks => TickClock.TickHz / 2;

    private readonly List<BeltItem> _buffer = new List<BeltItem>(BufferSlots);
    private readonly AddressFilter[] _filters = new AddressFilter[FilteredOutputs];
    private readonly List<BeltItem>[] _emitted =
    {
        new List<BeltItem>(),
        new List<BeltItem>(),
        new List<BeltItem>(),
        new List<BeltItem>()
    };
    private int _cooldown = ExaminePeriodTicks;

    public AddressSorter(TileCoord tile, Facing facing)
    {
        Tile = tile;
        Facing = facing;
        Ports = SorterPorts.Of(tile, facing);
    }

    public TileCoord Tile { get; }

    public Facing Facing { get; }

    public SorterPorts Ports { get; }

    public int BufferCount => _buffer.Count;

    public IReadOnlyList<BeltItem> Buffer => _buffer;

    public void SetFilter(SorterOutput output, AddressFilter filter)
    {
        int i = (int)output;
        if ((uint)i >= FilteredOutputs)
            throw new ArgumentOutOfRangeException(nameof(output), output, null);
        _filters[i] = filter;
    }

    public AddressFilter Filter(SorterOutput output)
    {
        int i = (int)output;
        if ((uint)i >= FilteredOutputs)
            throw new ArgumentOutOfRangeException(nameof(output), output, null);
        return _filters[i];
    }

    public bool TryAccept(in BeltItem item)
    {
        if (_buffer.Count >= BufferSlots) return false;
        _buffer.Add(item);
        return true;
    }

    public SorterOutput Route(in BeltItem item)
    {
        for (int i = 0; i < FilteredOutputs; i++)
        {
            if (_filters[i].Matches(item))
                return (SorterOutput)i;
        }

        return SorterOutput.Overflow;
    }

    public IReadOnlyList<BeltItem> Emitted(SorterOutput output)
    {
        int i = (int)output;
        if ((uint)i > (int)SorterOutput.Overflow)
            throw new ArgumentOutOfRangeException(nameof(output), output, null);
        return _emitted[i];
    }

    public void Step(Func<SorterOutput, BeltItem, bool>? emit = null)
    {
        if (_buffer.Count == 0) return;
        if (_cooldown > 0)
        {
            _cooldown--;
            if (_cooldown > 0) return;
        }

        var item = _buffer[0];
        var dest = Route(item);
        if (emit != null && !emit(dest, item)) return;
        _buffer.RemoveAt(0);
        _emitted[(int)dest].Add(item);
        _cooldown = ExaminePeriodTicks;
    }

    public void StepTicks(int ticks, Func<SorterOutput, BeltItem, bool>? emit = null)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks), ticks, null);
        for (int n = 0; n < ticks; n++)
            Step(emit);
    }
}

public readonly record struct SorterPorts(
    TileCoord Input,
    Facing InputTravel,
    TileCoord Overflow,
    Facing OverflowOut,
    TileCoord Left,
    Facing LeftOut,
    TileCoord Forward,
    Facing ForwardOut,
    TileCoord Right,
    Facing RightOut)
{
    public static SorterPorts Of(TileCoord origin, Facing facing)
    {
        Facing back = Opposite(facing);
        Facing left = LeftOf(facing);
        Facing right = RightOf(facing);
        FacePair(origin, back, out var input, out var overflow);
        FacePair(origin, facing, out var forward, out _);
        FacePair(origin, left, out var leftTile, out _);
        FacePair(origin, right, out var rightTile, out _);
        return new SorterPorts(input, facing, overflow, back, leftTile, left, forward, facing, rightTile, right);
    }

    public TileCoord Neighbor(SorterOutput output)
    {
        switch (output)
        {
            case SorterOutput.Overflow: return BeltNetwork.Next(Overflow, OverflowOut);
            case SorterOutput.Left: return BeltNetwork.Next(Left, LeftOut);
            case SorterOutput.Forward: return BeltNetwork.Next(Forward, ForwardOut);
            case SorterOutput.Right: return BeltNetwork.Next(Right, RightOut);
            default:
                throw new ArgumentOutOfRangeException(nameof(output), output, null);
        }
    }

    public Facing Outward(SorterOutput output)
    {
        switch (output)
        {
            case SorterOutput.Overflow: return OverflowOut;
            case SorterOutput.Left: return LeftOut;
            case SorterOutput.Forward: return ForwardOut;
            case SorterOutput.Right: return RightOut;
            default:
                throw new ArgumentOutOfRangeException(nameof(output), output, null);
        }
    }

    private static void FacePair(TileCoord origin, Facing face, out TileCoord first, out TileCoord second)
    {
        switch (face)
        {
            case Facing.North:
                first = new TileCoord(origin.X, origin.Y + 1);
                second = new TileCoord(origin.X + 1, origin.Y + 1);
                return;
            case Facing.East:
                first = new TileCoord(origin.X + 1, origin.Y);
                second = new TileCoord(origin.X + 1, origin.Y + 1);
                return;
            case Facing.South:
                first = new TileCoord(origin.X, origin.Y);
                second = new TileCoord(origin.X + 1, origin.Y);
                return;
            case Facing.West:
                first = new TileCoord(origin.X, origin.Y);
                second = new TileCoord(origin.X, origin.Y + 1);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(face), face, null);
        }
    }

    private static Facing LeftOf(Facing facing) => (Facing)(((int)facing + 3) & 3);

    private static Facing RightOf(Facing facing) => (Facing)(((int)facing + 1) & 3);

    private static Facing Opposite(Facing facing) => (Facing)(((int)facing + 2) & 3);
}

public sealed class AddressSorterNetwork
{
    private readonly List<WiredSorter> _sorters = new List<WiredSorter>();

    public IReadOnlyList<AddressSorter> Sorters
    {
        get
        {
            var rows = new AddressSorter[_sorters.Count];
            for (int i = 0; i < _sorters.Count; i++)
                rows[i] = _sorters[i].Machine;
            return rows;
        }
    }

    public void Compile(IReadOnlyList<ConstructRecord> constructs, BeltNetwork belts)
    {
        if (constructs is null) throw new ArgumentNullException(nameof(constructs));
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        _sorters.Clear();

        var rows = new List<ConstructRecord>();
        for (int i = 0; i < constructs.Count; i++)
        {
            var row = constructs[i];
            if (string.Equals(row.DefId, AddressSorter.BuildingId, StringComparison.Ordinal))
                rows.Add(row);
        }

        rows.Sort((a, b) =>
        {
            int byX = a.Tile.X.CompareTo(b.Tile.X);
            return byX != 0 ? byX : a.Tile.Y.CompareTo(b.Tile.Y);
        });

        for (int i = 0; i < rows.Count; i++)
            _sorters.Add(Wire(rows[i], belts));
    }

    public bool SetFilter(TileCoord tile, SorterOutput output, AddressFilter filter)
    {
        for (int i = 0; i < _sorters.Count; i++)
        {
            var wired = _sorters[i];
            if (!wired.Machine.Tile.Equals(tile)) continue;
            wired.Machine.SetFilter(output, filter);
            return true;
        }

        return false;
    }

    public void Step(BeltNetwork belts)
    {
        if (belts is null) throw new ArgumentNullException(nameof(belts));
        for (int i = 0; i < _sorters.Count; i++)
            _sorters[i].Step();
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

    private static WiredSorter Wire(ConstructRecord row, BeltNetwork belts)
    {
        var machine = new AddressSorter(row.Tile, row.Rotation);
        var ports = machine.Ports;
        var input = FindEnd(belts, ports.Input);
        input?.MarkJunctionInput();
        return new WiredSorter(
            machine,
            input,
            FindStart(belts, ports.Neighbor(SorterOutput.Left), ports.LeftOut),
            FindStart(belts, ports.Neighbor(SorterOutput.Forward), ports.ForwardOut),
            FindStart(belts, ports.Neighbor(SorterOutput.Right), ports.RightOut),
            FindStart(belts, ports.Neighbor(SorterOutput.Overflow), ports.OverflowOut));
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

    private sealed class WiredSorter
    {
        private readonly BeltSegment? _input;
        private readonly BeltSegment?[] _outputs;

        public WiredSorter(
            AddressSorter machine,
            BeltSegment? input,
            BeltSegment? left,
            BeltSegment? forward,
            BeltSegment? right,
            BeltSegment? overflow)
        {
            Machine = machine;
            _input = input;
            _outputs = new[] { left, forward, right, overflow };
        }

        public AddressSorter Machine { get; }

        public void Step()
        {
            Pull();
            Machine.Step(TryEmit);
        }

        private void Pull()
        {
            if (_input is null) return;
            for (int lane = 0; lane < BeltNetwork.LaneCount; lane++)
            {
                if (Machine.BufferCount >= AddressSorter.BufferSlots) return;
                if (!_input.TryPeekHead(lane, out var item)) continue;
                if (!Machine.TryAccept(item)) return;
                _input.TryTakeHead(lane, out _);
            }
        }

        private bool TryEmit(SorterOutput dest, BeltItem item)
        {
            var segment = _outputs[(int)dest];
            if (segment is null) return true;
            if (segment.TryInsert(0, item.ItemId, 0f, item.Kind, item.Address))
                return true;
            return !item.Kind.Equals(MailKinds.Cargo)
                && segment.TryInsert(1, item.ItemId, 0f, item.Kind, item.Address);
        }
    }
}
