using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Client.UI;

public readonly record struct CompassFrame(
    byte FacingEighth,
    string FacingLabel,
    byte CurrentDistrict,
    IReadOnlyList<byte> Slots)
{
    public const int SlotCount = 24;
    public const int NearbyRadiusCm = 8000;

    public static CompassFrame Empty { get; } = new(0, WindName(0), 0, Zeros());

    public string SlotKey
    {
        get
        {
            var chars = new char[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                byte district = SlotAt(i);
                chars[i] = district < 10
                    ? (char)('0' + district)
                    : (char)('A' + (district - 10) % 26);
            }

            return new string(chars);
        }
    }

    public static bool SameDisplay(in CompassFrame a, in CompassFrame b)
    {
        if (a.FacingEighth != b.FacingEighth || a.CurrentDistrict != b.CurrentDistrict)
            return false;
        for (int i = 0; i < SlotCount; i++)
        {
            if (a.SlotAt(i) != b.SlotAt(i))
                return false;
        }

        return true;
    }

    public static CompassFrame From(WorldTables? world, in PlayerPose pose)
    {
        byte eighth = Eighth(pose.Yaw);
        var slots = Zeros();
        if (world is null)
            return new CompassFrame(eighth, WindName(eighth), 0, slots);

        var nearest = new long[SlotCount];
        for (int i = 0; i < SlotCount; i++)
            nearest[i] = long.MaxValue;

        byte current = 0;
        long currentDist = long.MaxValue;
        long radiusSq = (long)NearbyRadiusCm * NearbyRadiusCm;
        double yawRad = pose.Yaw * (Math.PI * 2.0 / 65536.0);
        int half = world.TileCm / 2;

        var streets = world.Streets;
        for (int s = 0; s < streets.Length; s++)
        {
            var street = streets[s];
            var tiles = street.Tiles;
            if (tiles is null)
                continue;

            for (int t = 0; t < tiles.Length; t++)
            {
                var tile = tiles[t];
                int xcm = tile.X * world.TileCm + half;
                int ycm = tile.Y * world.TileCm + half;
                long dx = xcm - pose.Xcm;
                long dy = ycm - pose.Ycm;
                long distSq = dx * dx + dy * dy;
                if (distSq > radiusSq)
                    continue;

                if (distSq < currentDist)
                {
                    currentDist = distSq;
                    current = street.District;
                }

                if (distSq == 0)
                    continue;

                double relative = Math.Atan2(dx, dy) - yawRad;
                while (relative < -Math.PI)
                    relative += Math.PI * 2.0;
                while (relative >= Math.PI)
                    relative -= Math.PI * 2.0;

                int slot = (int)((relative + Math.PI) * SlotCount / (Math.PI * 2.0));
                if (slot < 0)
                    slot = 0;
                if (slot >= SlotCount)
                    slot = SlotCount - 1;
                if (distSq >= nearest[slot])
                    continue;

                nearest[slot] = distSq;
                slots[slot] = street.District;
            }
        }

        return new CompassFrame(eighth, WindName(eighth), current, slots);
    }

    public static byte Eighth(ushort yaw) => (byte)((uint)(yaw + 4096) / 8192 % 8);

    public static string WindName(byte eighth) => eighth switch
    {
        0 => "N",
        1 => "NE",
        2 => "E",
        3 => "SE",
        4 => "S",
        5 => "SW",
        6 => "W",
        7 => "NW",
        _ => throw new ArgumentOutOfRangeException(nameof(eighth), eighth, null),
    };

    private byte SlotAt(int index)
    {
        if (Slots is null || index < 0 || index >= Slots.Count)
            return 0;
        return Slots[index];
    }

    private static byte[] Zeros() => new byte[SlotCount];
}
