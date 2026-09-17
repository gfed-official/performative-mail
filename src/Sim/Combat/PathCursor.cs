using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Combat;

internal sealed class PathCursor
{
    private readonly TileCoord[] _tiles;
    private readonly double[] _metresAt;
    private readonly double _tileMetres;
    private int _segment;

    public PathCursor(IReadOnlyList<TileCoord> tiles, int tileCm)
    {
        if (tiles is null) throw new ArgumentNullException(nameof(tiles));
        if (tiles.Count < 1) throw new ArgumentException("Path needs at least one tile.", nameof(tiles));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm), tileCm, null);

        _tiles = new TileCoord[tiles.Count];
        for (int i = 0; i < tiles.Count; i++)
            _tiles[i] = tiles[i];
        _tileMetres = tileCm / 100.0;

        // Snap to Tiles[0]. LengthMetres is the Manhattan polyline, not RoutePath.LengthTiles.
        _metresAt = new double[_tiles.Length];
        _metresAt[0] = 0;
        for (int i = 1; i < _tiles.Length; i++)
            _metresAt[i] = _metresAt[i - 1] + Manhattan(_tiles[i - 1], _tiles[i]) * _tileMetres;

        _segment = 0;
        MetresAlong = 0;
    }

    public double MetresAlong { get; private set; }

    public double LengthMetres => _metresAt[_metresAt.Length - 1];

    public bool AtEnd => MetresAlong >= LengthMetres;

    public void Advance(double metres)
    {
        if (metres < 0 || double.IsNaN(metres))
            throw new ArgumentOutOfRangeException(nameof(metres), metres, null);

        MetresAlong = Math.Min(MetresAlong + metres, LengthMetres);
        while (_segment < _tiles.Length - 2 && MetresAlong >= _metresAt[_segment + 1])
            _segment++;
    }

    public PlayerPose Pose
    {
        get
        {
            if (_tiles.Length == 1)
            {
                var only = Centre(_tiles[0]);
                return PlayerPose.FromMeters(only.X, only.Y, 0, 0);
            }

            var a = Centre(_tiles[_segment]);
            var b = Centre(_tiles[_segment + 1]);
            double seg = _metresAt[_segment + 1] - _metresAt[_segment];
            double t = seg <= 0 ? 1 : Math.Clamp((MetresAlong - _metresAt[_segment]) / seg, 0, 1);
            return PlayerPose.FromMeters(Lerp(a.X, b.X, t), Lerp(a.Y, b.Y, t), 0, YawToward(a, b));
        }
    }

    private (double X, double Y) Centre(TileCoord tile) =>
        ((tile.X + 0.5) * _tileMetres, (tile.Y + 0.5) * _tileMetres);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static ushort YawToward((double X, double Y) from, (double X, double Y) to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return 0;
        double turns = Math.Atan2(dx, dy) / (Math.PI * 2.0);
        if (turns < 0) turns += 1;
        return (ushort)((int)Math.Round(turns * 65536.0, MidpointRounding.AwayFromZero) & 65535);
    }

    private static int Manhattan(TileCoord a, TileCoord b)
    {
        int dx = a.X - b.X;
        if (dx < 0) dx = -dx;
        int dy = a.Y - b.Y;
        if (dy < 0) dy = -dy;
        return dx + dy;
    }
}
