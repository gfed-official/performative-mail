using System;

namespace PerformativeMail.Sim.Net;

public static class MapPingCodec
{
    public static byte[] Encode(in MapPingRequest message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.MapPing);
        writer.WriteInt32(message.TileX);
        writer.WriteInt32(message.TileY);
        writer.WriteByte(message.Kind);
        return writer.ToArray();
    }

    public static byte[] Encode(in MapPingEvent message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.MapPingEvent);
        writer.WriteInt32(message.Id);
        writer.WriteInt32(message.TileX);
        writer.WriteInt32(message.TileY);
        writer.WriteByte(message.Kind);
        writer.WriteUInt32(message.PlacedTick);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out MapPingRequest message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.MapPing)) return false;
        if (!reader.TryReadInt32(out var tileX)) return false;
        if (!reader.TryReadInt32(out var tileY)) return false;
        if (!reader.TryReadByte(out var kind)) return false;
        if (!reader.AtEnd) return false;

        message = new MapPingRequest(tileX, tileY, kind);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out MapPingEvent message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.MapPingEvent)) return false;
        if (!reader.TryReadInt32(out var id)) return false;
        if (!reader.TryReadInt32(out var tileX)) return false;
        if (!reader.TryReadInt32(out var tileY)) return false;
        if (!reader.TryReadByte(out var kind)) return false;
        if (!reader.TryReadUInt32(out var placedTick)) return false;
        if (!reader.AtEnd) return false;

        message = new MapPingEvent(id, tileX, tileY, kind, placedTick);
        return true;
    }

    private static bool TryReadKind(BitReader reader, MessageKind expected)
    {
        if (!reader.TryReadByte(out var raw)) return false;
        return raw == (byte)expected;
    }
}
