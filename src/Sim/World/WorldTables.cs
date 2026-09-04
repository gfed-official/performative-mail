using System;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public sealed class WorldTables
{
    public WorldTables(
        int width,
        int height,
        int tileCm,
        short[] heights,
        AddressId[] addresses,
        bool[] buildable,
        bool valid,
        int heightmapAttempts)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (heightmapAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(heightmapAttempts));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (addresses is null) throw new ArgumentNullException(nameof(addresses));
        if (buildable is null) throw new ArgumentNullException(nameof(buildable));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));
        if (buildable.Length != heights.Length)
            throw new ArgumentException("Buildable buffer must match height buffer.", nameof(buildable));

        Width = width;
        Height = height;
        TileCm = tileCm;
        Heights = (short[])heights.Clone();
        Addresses = (AddressId[])addresses.Clone();
        Buildable = (bool[])buildable.Clone();
        Valid = valid;
        HeightmapAttempts = heightmapAttempts;
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCm { get; }

    public short[] Heights { get; }

    public AddressId[] Addresses { get; }

    public bool[] Buildable { get; }

    public bool Valid { get; }

    public int HeightmapAttempts { get; }
}
