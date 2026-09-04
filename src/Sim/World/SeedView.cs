using System;
using System.Text;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class SeedView
{
    public const int CellTiles = 10;

    public static string Render(uint seed, WorldTables tables, ulong worldHash)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));

        var text = new StringBuilder();
        text.Append("seed 0x").Append(seed.ToString("X8")).Append('\n');
        text.Append("worldHash 0x").Append(worldHash.ToString("X16")).Append('\n');
        text.Append("valid ").Append(tables.Valid ? "true" : "false").Append('\n');
        text.Append("width ").Append(tables.Width)
            .Append(" height ").Append(tables.Height)
            .Append(" tileCm ").Append(tables.TileCm).Append('\n');

        int districtMax = 0;
        var streets = tables.Streets;
        for (int i = 0; i < streets.Length; i++)
        {
            if (streets[i].District > districtMax)
                districtMax = streets[i].District;
        }

        var houses = tables.Houses;
        for (int i = 0; i < houses.Length; i++)
        {
            if (houses[i].Address.District > districtMax)
                districtMax = houses[i].Address.District;
        }

        text.Append("districts ").Append(districtMax).Append('\n');
        text.Append("streets\n");
        for (int i = 0; i < streets.Length; i++)
        {
            var street = streets[i];
            text.Append("  ").Append(street.District)
                .Append(' ').Append(street.Id)
                .Append(' ').Append(street.Name).Append('\n');
        }

        text.Append("addresses\n");
        var names = StreetNames(streets);
        var addresses = (AddressId[])tables.Addresses.Clone();
        Array.Sort(addresses, (a, b) => a.Packed.CompareTo(b.Packed));
        for (int i = 0; i < addresses.Length; i++)
        {
            var address = addresses[i];
            string name = address.Street < names.Length ? names[address.Street] : string.Empty;
            text.Append("  ")
                .Append(address.District).Append(':')
                .Append(address.Street).Append(':')
                .Append(address.Number)
                .Append("  ").Append(address.Number).Append(' ').Append(name).Append('\n');
        }

        text.Append("map\n");
        text.Append(RenderDistrictMap(tables));
        return text.ToString();
    }

    public static string RenderDistrictMap(WorldTables tables)
    {
        if (tables is null) throw new ArgumentNullException(nameof(tables));

        int cellsX = tables.Width / CellTiles;
        int cellsY = tables.Height / CellTiles;
        if (cellsX < 1) cellsX = 1;
        if (cellsY < 1) cellsY = 1;

        var marks = new byte[cellsX * cellsY];
        var streets = tables.Streets;
        for (int s = 0; s < streets.Length; s++)
        {
            var tiles = streets[s].Tiles;
            if (tiles is null) continue;
            for (int t = 0; t < tiles.Length; t++)
                Mark(marks, cellsX, cellsY, tiles[t].X, tiles[t].Y, streets[s].District);
        }

        var houses = tables.Houses;
        for (int h = 0; h < houses.Length; h++)
        {
            var lot = houses[h].Lot;
            for (int y = 0; y < lot.Height; y++)
            {
                for (int x = 0; x < lot.Width; x++)
                    Mark(marks, cellsX, cellsY, lot.X + x, lot.Y + y, houses[h].Address.District);
            }
        }

        var text = new StringBuilder(cellsY * (cellsX + 1));
        for (int cy = 0; cy < cellsY; cy++)
        {
            for (int cx = 0; cx < cellsX; cx++)
            {
                byte district = marks[cy * cellsX + cx];
                text.Append(district == 0 ? '.' : (char)('0' + district));
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    private static void Mark(byte[] marks, int cellsX, int cellsY, int tileX, int tileY, byte district)
    {
        if (district == 0) return;
        int cx = tileX / CellTiles;
        int cy = tileY / CellTiles;
        if ((uint)cx >= (uint)cellsX || (uint)cy >= (uint)cellsY) return;
        marks[cy * cellsX + cx] = district;
    }

    private static string[] StreetNames(StreetRecord[] streets)
    {
        int max = 0;
        for (int i = 0; i < streets.Length; i++)
        {
            if (streets[i].Id > max)
                max = streets[i].Id;
        }

        var names = new string[max + 1];
        for (int i = 0; i < names.Length; i++)
            names[i] = string.Empty;
        for (int i = 0; i < streets.Length; i++)
            names[streets[i].Id] = streets[i].Name ?? string.Empty;
        return names;
    }
}
