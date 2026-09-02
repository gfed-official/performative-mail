using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public sealed class WorldAtlas
{
    public const int LatticeCm = 50;

    public const int TileCmDefault = 200;

    private readonly Dictionary<AddressId, HouseRecord> _houses;

    internal WorldAtlas(
        string id,
        int tileCm,
        byte districtId,
        byte streetId,
        string streetName,
        PostOfficeRecord postOffice,
        TileRect streetRect,
        IReadOnlyList<HouseRecord> houses)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Map id is required.", nameof(id));
        if (tileCm <= 0) throw new ArgumentOutOfRangeException(nameof(tileCm));
        if (string.IsNullOrWhiteSpace(streetName)) throw new ArgumentException("Street name is required.", nameof(streetName));
        if (houses is null) throw new ArgumentNullException(nameof(houses));

        Id = id;
        TileCm = tileCm;
        DistrictId = districtId;
        StreetId = streetId;
        StreetName = streetName;
        PostOffice = postOffice;
        StreetRect = streetRect;
        _houses = new Dictionary<AddressId, HouseRecord>(houses.Count);
        var addresses = new AddressId[houses.Count];
        for (int i = 0; i < houses.Count; i++)
        {
            var house = houses[i];
            _houses.Add(house.Address, house);
            addresses[i] = house.Address;
        }

        Array.Sort(addresses, CompareAddress);
        DeliverableAddresses = addresses;
        Houses = _houses;
    }

    public string Id { get; }

    public int TileCm { get; }

    public byte DistrictId { get; }

    public byte StreetId { get; }

    public string StreetName { get; }

    public PostOfficeRecord PostOffice { get; }

    public TileRect StreetRect { get; }

    public IReadOnlyDictionary<AddressId, HouseRecord> Houses { get; }

    public IReadOnlyList<AddressId> DeliverableAddresses { get; }

    public bool Walkable(TileCoord tile) =>
        StreetRect.Contains(tile) || tile == PostOffice.SpawnPadTile;

    public bool TryMailboxPose(AddressId address, out MailboxPose pose)
    {
        if (_houses.TryGetValue(address, out var house))
        {
            pose = house.Mailbox;
            return true;
        }

        pose = default;
        return false;
    }

    public bool MailboxReachesStreet(MailboxPose pose)
    {
        var tile = pose.Tile(TileCm);
        if (Walkable(tile)) return true;
        foreach (var neighbor in tile.EdgeNeighbors())
        {
            if (StreetRect.Contains(neighbor)) return true;
        }

        return false;
    }

    private static int CompareAddress(AddressId left, AddressId right)
    {
        int district = left.District.CompareTo(right.District);
        if (district != 0) return district;
        int street = left.Street.CompareTo(right.Street);
        if (street != 0) return street;
        int number = left.Number.CompareTo(right.Number);
        if (number != 0) return number;
        return left.Unit.CompareTo(right.Unit);
    }
}
