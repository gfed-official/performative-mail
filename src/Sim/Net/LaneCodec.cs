using System;
using PerformativeMail.Sim.Automation;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Net;

public static class LaneCodec
{
    public static byte[] Encode(LaneInsert message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.LaneInsert);
        writer.WriteUInt64(message.Segment.Value);
        writer.WriteByte(message.Lane);
        writer.WriteUInt16(message.ItemKind.Value);
        writer.WriteByte(message.Colour.District);
        writer.WriteByte(message.Colour.Street);
        writer.WriteInt32(message.PositionAtTickCm);
        return writer.ToArray();
    }

    public static byte[] Encode(LaneRemove message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.LaneRemove);
        writer.WriteUInt64(message.Segment.Value);
        writer.WriteByte(message.Lane);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out LaneInsert message)
    {
        message = null!;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.LaneInsert)) return false;
        if (!reader.TryReadUInt64(out var segment)) return false;
        if (!TryReadLane(reader, out var lane)) return false;
        if (!reader.TryReadUInt16(out var kind)) return false;
        if (!reader.TryReadByte(out var district)) return false;
        if (!reader.TryReadByte(out var street)) return false;
        if (!reader.TryReadInt32(out var positionCm)) return false;
        if (positionCm < 0) return false;
        if (!reader.AtEnd) return false;

        message = new LaneInsert(
            new SegmentId(segment),
            lane,
            new MailKindId(kind),
            new AddressColour(district, street),
            positionCm);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out LaneRemove message)
    {
        message = null!;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.LaneRemove)) return false;
        if (!reader.TryReadUInt64(out var segment)) return false;
        if (!TryReadLane(reader, out var lane)) return false;
        if (!reader.AtEnd) return false;

        message = new LaneRemove(new SegmentId(segment), lane);
        return true;
    }

    private static bool TryReadKind(BitReader reader, MessageKind expected)
    {
        if (!reader.TryReadByte(out var raw)) return false;
        return raw == (byte)expected;
    }

    private static bool TryReadLane(BitReader reader, out byte lane)
    {
        lane = 0;
        if (!reader.TryReadByte(out var raw)) return false;
        if (raw > 1) return false;
        lane = raw;
        return true;
    }
}
