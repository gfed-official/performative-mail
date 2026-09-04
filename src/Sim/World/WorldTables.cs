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
        HouseRecord[] houses,
        bool[] buildable,
        bool valid,
        int heightmapAttempts,
        PostOfficeRecord postOffice,
        StreetRecord[] streets,
        LotRecord[] lots)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (heightmapAttempts <= 0) throw new ArgumentOutOfRangeException(nameof(heightmapAttempts));
        if (heights is null) throw new ArgumentNullException(nameof(heights));
        if (houses is null) throw new ArgumentNullException(nameof(houses));
        if (buildable is null) throw new ArgumentNullException(nameof(buildable));
        if (streets is null) throw new ArgumentNullException(nameof(streets));
        if (lots is null) throw new ArgumentNullException(nameof(lots));
        if (heights.Length != width * height)
            throw new ArgumentException("Height buffer must be width × height.", nameof(heights));
        if (buildable.Length != heights.Length)
            throw new ArgumentException("Buildable buffer must match height buffer.", nameof(buildable));

        Width = width;
        Height = height;
        TileCm = tileCm;
        Heights = (short[])heights.Clone();
        Houses = (HouseRecord[])houses.Clone();
        Addresses = AddressesOf(Houses);
        Buildable = (bool[])buildable.Clone();
        Valid = valid;
        HeightmapAttempts = heightmapAttempts;
        PostOffice = postOffice;
        Streets = CloneStreets(streets);
        Lots = (LotRecord[])lots.Clone();
    }

    public int Width { get; }

    public int Height { get; }

    public int TileCm { get; }

    public short[] Heights { get; }

    public AddressId[] Addresses { get; }

    public HouseRecord[] Houses { get; }

    public bool[] Buildable { get; }

    public bool Valid { get; }

    public int HeightmapAttempts { get; }

    public PostOfficeRecord PostOffice { get; }

    public StreetRecord[] Streets { get; }

    public LotRecord[] Lots { get; }

    private static AddressId[] AddressesOf(HouseRecord[] houses)
    {
        var addresses = new AddressId[houses.Length];
        for (int i = 0; i < houses.Length; i++)
            addresses[i] = houses[i].Address;
        Array.Sort(addresses, (a, b) => a.Packed.CompareTo(b.Packed));
        return addresses;
    }

    private static StreetRecord[] CloneStreets(StreetRecord[] streets)
    {
        var copy = new StreetRecord[streets.Length];
        for (int i = 0; i < streets.Length; i++)
        {
            var street = streets[i];
            var tiles = street.Tiles is null ? Array.Empty<TileCoord>() : (TileCoord[])street.Tiles.Clone();
            copy[i] = new StreetRecord(street.Id, street.Name, street.District, tiles);
        }

        return copy;
    }
}
