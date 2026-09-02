using System.Collections.Generic;

namespace PerformativeMail.Sim.Inventory;

public readonly record struct Footprint(byte W, byte H)
{
    public Footprint Rotated => new(H, W);

    public bool IsSquare => W == H;

    public int Area => W * H;
}

public readonly record struct Cell(byte X, byte Y);

public readonly record struct CellRect(byte X, byte Y, byte W, byte H)
{
    public IEnumerable<Cell> Cells()
    {
        int x1 = X + W;
        int y1 = Y + H;
        for (int y = Y; y < y1; y++)
        for (int x = X; x < x1; x++)
            yield return new Cell((byte)x, (byte)y);
    }

    public bool Overlaps(CellRect other) =>
        X < other.X + other.W && other.X < X + W &&
        Y < other.Y + other.H && other.Y < Y + H;
}

public readonly record struct Placement(byte X, byte Y, bool Rotated)
{
    public static readonly Placement Origin = new(0, 0, false);

    public static Placement For(Footprint footprint, byte x, byte y, bool rotated)
        => new(x, y, rotated && !footprint.IsSquare);
}

public readonly struct ContainerShape
{
    public readonly byte Cols;
    public readonly byte Rows;
    public readonly bool IgnoresFootprint;
    private readonly ulong[]? _blocked;

    private ContainerShape(byte cols, byte rows, bool ignoresFootprint, ulong[]? blocked)
    {
        Cols = cols;
        Rows = rows;
        IgnoresFootprint = ignoresFootprint;
        _blocked = blocked;
    }

    public static ContainerShape Grid(byte cols, byte rows, params Cell[] blocked)
        => new(cols, rows, ignoresFootprint: false, PackBlocked(cols, rows, blocked));

    public static readonly ContainerShape Slot = new(1, 1, ignoresFootprint: true, null);

    public int CellCount => Cols * Rows;

    public bool IsBlocked(Cell cell)
    {
        if (_blocked is null) return false;
        int i = cell.Y * Cols + cell.X;
        if ((uint)i >= (uint)CellCount) return false;
        return (_blocked[i >> 6] & (1UL << (i & 63))) != 0;
    }

    public bool TryRect(Placement at, Footprint footprint, out CellRect rect)
    {
        if (IgnoresFootprint)
        {
            if (at.X != 0 || at.Y != 0)
            {
                rect = default;
                return false;
            }

            rect = new CellRect(0, 0, 1, 1);
            return true;
        }

        int w = at.Rotated ? footprint.H : footprint.W;
        int h = at.Rotated ? footprint.W : footprint.H;
        if (at.X + w > Cols || at.Y + h > Rows)
        {
            rect = default;
            return false;
        }

        rect = new CellRect(at.X, at.Y, (byte)w, (byte)h);
        foreach (var cell in rect.Cells())
        {
            if (!IsBlocked(cell)) continue;
            rect = default;
            return false;
        }

        return true;
    }

    private static ulong[]? PackBlocked(byte cols, byte rows, Cell[]? blocked)
    {
        if (blocked is null || blocked.Length == 0) return null;
        int cellCount = cols * rows;
        var bits = new ulong[(cellCount + 63) / 64];
        foreach (var cell in blocked)
        {
            int i = cell.Y * cols + cell.X;
            if ((uint)i >= (uint)cellCount) continue;
            bits[i >> 6] |= 1UL << (i & 63);
        }

        return bits;
    }
}
