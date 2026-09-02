using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.World;

public static class WorldAtlasLoader
{
    public const string TestMapRelativePath = "world/m0_test_map.json";
    public const string ExpectedStreetName = "Larch Lane";
    public const int ExpectedHouseCount = 10;
    public const int ExpectedLotSize = 4;
    public const int ExpectedPoSize = 6;
    public const int InteractRangeCm = 250;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static WorldAtlas LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new WorldAtlasException($"Could not read '{path}'.", ex);
        }

        return LoadJson(json, path);
    }

    public static WorldAtlas LoadFromContentRoot(string contentRoot)
        => LoadFile(Path.Combine(contentRoot, TestMapRelativePath));

    public static WorldAtlas LoadJson(string json, string source)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));
        if (string.IsNullOrWhiteSpace(source)) source = "map";

        MapDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<MapDocument>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new WorldAtlasException($"{source}: invalid JSON. {ex.Message}", ex);
        }

        if (doc is null)
            throw new WorldAtlasException($"{source}: document is empty.");

        if (string.IsNullOrWhiteSpace(doc.Id))
            throw new WorldAtlasException($"{source}: id is required.");
        if (doc.TileCm != WorldAtlas.TileCmDefault)
            throw new WorldAtlasException($"{source}: tileCm must be {WorldAtlas.TileCmDefault}.");
        if (doc.DistrictId != 1)
            throw new WorldAtlasException($"{source}: districtId must be 1.");
        if (doc.Street is null)
            throw new WorldAtlasException($"{source}: street is required.");
        if (doc.Street.Id != 1)
            throw new WorldAtlasException($"{source}: street.id must be 1.");
        if (doc.Street.Name != ExpectedStreetName)
            throw new WorldAtlasException($"{source}: street.name must be '{ExpectedStreetName}'.");
        if (doc.PostOffice is null)
            throw new WorldAtlasException($"{source}: postOffice is required.");
        if (doc.StreetTiles is null)
            throw new WorldAtlasException($"{source}: streetTiles is required.");
        if (doc.Houses is null)
            throw new WorldAtlasException($"{source}: houses is required.");

        var streetRect = ReadRect(doc.StreetTiles, "streetTiles", source);
        var postOffice = ReadPostOffice(doc.PostOffice, source);
        if (streetRect.Overlaps(postOffice.Footprint))
            throw new WorldAtlasException($"{source}: street overlaps the post office.");

        var houses = ReadHouses(doc.Houses, doc.DistrictId, doc.Street.Id, streetRect, postOffice.Footprint, source);
        return new WorldAtlas(
            doc.Id,
            doc.TileCm,
            doc.DistrictId,
            doc.Street.Id,
            doc.Street.Name,
            postOffice,
            streetRect,
            houses);
    }

    private static PostOfficeRecord ReadPostOffice(PostOfficeDocument po, string source)
    {
        var tile = ReadCoord(po.Tile, "postOffice.tile", source);
        var size = ReadCoord(po.SizeTiles, "postOffice.sizeTiles", source);
        if (size.X != ExpectedPoSize || size.Y != ExpectedPoSize)
            throw new WorldAtlasException($"{source}: post office size must be {ExpectedPoSize}x{ExpectedPoSize}.");

        var footprint = new TileRect(tile.X, tile.Y, size.X, size.Y);
        footprint.RequirePositive("postOffice.sizeTiles", source);
        var spawn = ReadCoord(po.SpawnPadTile, "postOffice.spawnPadTile", source);
        var intake = ReadCoord(po.IntakeTile, "postOffice.intakeTile", source);
        if (!footprint.Contains(spawn))
            throw new WorldAtlasException($"{source}: spawn pad {spawn} is outside the post office.");
        if (!footprint.Contains(intake))
            throw new WorldAtlasException($"{source}: intake tile {intake} is outside the post office.");

        var face = FacingConversions.Parse(po.IntakeFace);
        if (face != Facing.East)
            throw new WorldAtlasException($"{source}: intakeFace must be east.");

        return new PostOfficeRecord(tile, size, spawn, intake, face);
    }

    private static IReadOnlyList<HouseRecord> ReadHouses(
        HouseDocument[] houses,
        byte districtId,
        byte streetId,
        TileRect street,
        TileRect postOffice,
        string source)
    {
        if (houses.Length != ExpectedHouseCount)
            throw new WorldAtlasException($"{source}: expected {ExpectedHouseCount} houses, found {houses.Length}.");

        var seen = new HashSet<int>();
        for (int i = 0; i < houses.Length; i++)
        {
            var probe = houses[i] ?? throw new WorldAtlasException($"{source}: houses[{i}] is null.");
            if (probe.Number < 1 || probe.Number > ExpectedHouseCount)
                throw new WorldAtlasException($"{source}: house number {probe.Number} is outside 1..{ExpectedHouseCount}.");
            if (!seen.Add(probe.Number))
                throw new WorldAtlasException($"{source}: duplicate house number {probe.Number}.");
        }

        for (int n = 1; n <= ExpectedHouseCount; n++)
        {
            if (!seen.Contains(n))
                throw new WorldAtlasException($"{source}: missing house number {n}.");
        }

        var records = new HouseRecord[houses.Length];
        int? oddY = null;
        int? evenY = null;
        int lastOddX = int.MinValue;
        int lastEvenX = int.MinValue;

        for (int i = 0; i < houses.Length; i++)
        {
            var raw = houses[i]!;
            if (raw.Unit != 0)
                throw new WorldAtlasException($"{source}: house {raw.Number} unit must be 0.");

            var lotTile = ReadCoord(raw.LotTile, $"houses[{i}].lotTile", source);
            var lotSize = ReadCoord(raw.LotSizeTiles, $"houses[{i}].lotSizeTiles", source);
            if (lotSize.X != ExpectedLotSize || lotSize.Y != ExpectedLotSize)
                throw new WorldAtlasException($"{source}: house {raw.Number} lot must be {ExpectedLotSize}x{ExpectedLotSize}.");

            var lot = new TileRect(lotTile.X, lotTile.Y, lotSize.X, lotSize.Y);
            lot.RequirePositive($"house {raw.Number} lot", source);
            if (lot.Overlaps(street))
                throw new WorldAtlasException($"{source}: house {raw.Number} lot overlaps the street.");
            if (lot.Overlaps(postOffice))
                throw new WorldAtlasException($"{source}: house {raw.Number} lot overlaps the post office.");

            for (int j = 0; j < i; j++)
            {
                if (lot.Overlaps(records[j].Lot))
                    throw new WorldAtlasException($"{source}: house {raw.Number} lot overlaps house {records[j].Address.Number}.");
            }

            var mailbox = ReadMailbox(raw, i, source);
            if (!mailbox.OnLattice(WorldAtlas.LatticeCm))
                throw new WorldAtlasException($"{source}: house {raw.Number} mailbox is not on the {WorldAtlas.LatticeCm} cm lattice.");

            var mailboxTile = mailbox.Tile(WorldAtlas.TileCmDefault);
            bool onStreetEdge = lot.Contains(mailboxTile) || OnLotBoundary(lot, mailbox, WorldAtlas.TileCmDefault);
            if (!onStreetEdge)
                throw new WorldAtlasException($"{source}: house {raw.Number} mailbox is not on the lot.");

            bool northOfStreet = lot.Y >= street.MaxY;
            bool southOfStreet = lot.MaxY <= street.Y;
            if (northOfStreet == southOfStreet)
                throw new WorldAtlasException($"{source}: house {raw.Number} must sit on one side of the street.");

            if ((raw.Number & 1) == 1)
            {
                if (!northOfStreet)
                    throw new WorldAtlasException($"{source}: odd houses must sit north of the street.");
                if (oddY is null) oddY = lot.Y;
                else if (oddY.Value != lot.Y)
                    throw new WorldAtlasException($"{source}: odd houses must share one street side.");
                if (lot.X < lastOddX)
                    throw new WorldAtlasException($"{source}: odd houses must number outward from the town centre.");
                lastOddX = lot.X;
            }
            else
            {
                if (!southOfStreet)
                    throw new WorldAtlasException($"{source}: even houses must sit south of the street.");
                if (evenY is null) evenY = lot.Y;
                else if (evenY.Value != lot.Y)
                    throw new WorldAtlasException($"{source}: even houses must share one street side.");
                if (lot.X < lastEvenX)
                    throw new WorldAtlasException($"{source}: even houses must number outward from the town centre.");
                lastEvenX = lot.X;
            }

            var facing = FacingConversions.FromYawDegrees(mailbox.YawDegrees);
            var towardStreet = northOfStreet ? Facing.South : Facing.North;
            if (facing != towardStreet)
                throw new WorldAtlasException($"{source}: house {raw.Number} mailbox must face the street.");

            if (!MailboxNearStreet(mailbox, street, WorldAtlas.TileCmDefault))
                throw new WorldAtlasException($"{source}: house {raw.Number} mailbox is more than {InteractRangeCm} cm from the street.");

            records[i] = new HouseRecord(
                new AddressId(districtId, streetId, (byte)raw.Number, 0),
                lotTile,
                lotSize,
                mailbox);
        }

        if (oddY is null || evenY is null || oddY.Value == evenY.Value)
            throw new WorldAtlasException($"{source}: odd and even houses must sit on opposite sides of the street.");

        return records;
    }

    private static MailboxPose ReadMailbox(HouseDocument raw, int index, string source)
    {
        var cm = raw.MailboxCm;
        if (cm is null || cm.Length != 3)
            throw new WorldAtlasException($"{source}: houses[{index}].mailboxCm must be [x, y, z].");
        return new MailboxPose(cm[0], cm[1], cm[2], raw.MailboxYaw);
    }

    private static bool OnLotBoundary(TileRect lot, MailboxPose mailbox, int tileCm)
    {
        int minX = lot.X * tileCm;
        int minY = lot.Y * tileCm;
        int maxX = lot.MaxX * tileCm;
        int maxY = lot.MaxY * tileCm;
        bool onX = mailbox.XCm >= minX && mailbox.XCm <= maxX;
        bool onY = mailbox.YCm >= minY && mailbox.YCm <= maxY;
        if (!onX || !onY) return false;
        return mailbox.XCm == minX || mailbox.XCm == maxX || mailbox.YCm == minY || mailbox.YCm == maxY;
    }

    private static bool MailboxNearStreet(MailboxPose mailbox, TileRect street, int tileCm)
    {
        int minX = street.X * tileCm;
        int minY = street.Y * tileCm;
        int maxX = street.MaxX * tileCm;
        int maxY = street.MaxY * tileCm;
        int dx = 0;
        if (mailbox.XCm < minX) dx = minX - mailbox.XCm;
        else if (mailbox.XCm > maxX) dx = mailbox.XCm - maxX;
        int dy = 0;
        if (mailbox.YCm < minY) dy = minY - mailbox.YCm;
        else if (mailbox.YCm > maxY) dy = mailbox.YCm - maxY;
        long distSq = (long)dx * dx + (long)dy * dy;
        return distSq <= (long)InteractRangeCm * InteractRangeCm;
    }

    private static TileRect ReadRect(RectDocument rect, string field, string source)
    {
        var tile = ReadCoord(rect.Tile, field + ".tile", source);
        var size = ReadCoord(rect.SizeTiles, field + ".sizeTiles", source);
        var result = new TileRect(tile.X, tile.Y, size.X, size.Y);
        result.RequirePositive(field, source);
        return result;
    }

    private static TileCoord ReadCoord(int[]? value, string field, string source)
    {
        if (value is null || value.Length != 2)
            throw new WorldAtlasException($"{source}: {field} must be [x, y].");
        return new TileCoord(value[0], value[1]);
    }

    private sealed class MapDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("districtId")]
        public byte DistrictId { get; set; }

        [JsonPropertyName("tileCm")]
        public int TileCm { get; set; }

        [JsonPropertyName("street")]
        public StreetDocument? Street { get; set; }

        [JsonPropertyName("postOffice")]
        public PostOfficeDocument? PostOffice { get; set; }

        [JsonPropertyName("houses")]
        public HouseDocument[]? Houses { get; set; }

        [JsonPropertyName("streetTiles")]
        public RectDocument? StreetTiles { get; set; }
    }

    private sealed class StreetDocument
    {
        [JsonPropertyName("id")]
        public byte Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class PostOfficeDocument
    {
        [JsonPropertyName("tile")]
        public int[]? Tile { get; set; }

        [JsonPropertyName("sizeTiles")]
        public int[]? SizeTiles { get; set; }

        [JsonPropertyName("spawnPadTile")]
        public int[]? SpawnPadTile { get; set; }

        [JsonPropertyName("intakeTile")]
        public int[]? IntakeTile { get; set; }

        [JsonPropertyName("intakeFace")]
        public string? IntakeFace { get; set; }
    }

    private sealed class HouseDocument
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("unit")]
        public byte Unit { get; set; }

        [JsonPropertyName("lotTile")]
        public int[]? LotTile { get; set; }

        [JsonPropertyName("lotSizeTiles")]
        public int[]? LotSizeTiles { get; set; }

        [JsonPropertyName("mailboxCm")]
        public int[]? MailboxCm { get; set; }

        [JsonPropertyName("mailboxYaw")]
        public int MailboxYaw { get; set; }
    }

    private sealed class RectDocument
    {
        [JsonPropertyName("tile")]
        public int[]? Tile { get; set; }

        [JsonPropertyName("sizeTiles")]
        public int[]? SizeTiles { get; set; }
    }
}
