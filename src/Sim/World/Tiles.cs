using System.Collections.Generic;

namespace PerformativeMail.Sim.World;

public readonly record struct TileCoord(int X, int Y)
{
    public bool SharesEdgeWith(TileCoord other) =>
        (X == other.X && Abs(Y - other.Y) == 1) ||
        (Y == other.Y && Abs(X - other.X) == 1);

    public IEnumerable<TileCoord> EdgeNeighbors()
    {
        yield return new TileCoord(X - 1, Y);
        yield return new TileCoord(X + 1, Y);
        yield return new TileCoord(X, Y - 1);
        yield return new TileCoord(X, Y + 1);
    }

    private static int Abs(int value) => value < 0 ? -value : value;
}

public readonly record struct TileRect(int X, int Y, int Width, int Height)
{
    public int MaxX => X + Width;

    public int MaxY => Y + Height;

    public bool Contains(TileCoord tile) =>
        tile.X >= X && tile.X < MaxX && tile.Y >= Y && tile.Y < MaxY;

    public bool Overlaps(TileRect other) =>
        X < other.MaxX && other.X < MaxX &&
        Y < other.MaxY && other.Y < MaxY;

    public IEnumerable<TileCoord> Tiles()
    {
        for (int y = Y; y < MaxY; y++)
        for (int x = X; x < MaxX; x++)
            yield return new TileCoord(x, y);
    }

    public void RequirePositive(string name, string source)
    {
        if (Width <= 0 || Height <= 0)
            throw new WorldAtlasException($"{source}: {name} size must be positive.");
    }
}
