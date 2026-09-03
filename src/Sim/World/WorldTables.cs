using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public sealed class WorldTables
{
    public WorldTables(int width, int height, int tileCm, short[] heights, AddressId[] addresses)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (addresses is null) throw new ArgumentNullException(nameof(addresses));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));

        Width = width;
        Height = height;
        TileCm = tileCm;
        Heights = (short[])heights.Clone();
        Addresses = (AddressId[])addresses.Clone();
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCm { get; }

    public short[] Heights { get; }

    public AddressId[] Addresses { get; }
}
