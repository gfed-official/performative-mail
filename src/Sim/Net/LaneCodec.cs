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

    public static byte[] Encode(LaneChecksum message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.LaneChecksum);
        writer.WriteUInt64(message.Segment.Value);
        writer.WriteByte(message.Lane);
        writer.WriteUInt16(message.Count);
        writer.WriteUInt32(message.Hash);
        return writer.ToArray();
    }

    public static byte[] Encode(LaneState message)
    {
        if (message.Items is null) throw new ArgumentNullException(nameof(message));

        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.LaneState);
        writer.WriteUInt64(message.Segment.Value);
        writer.WriteByte(message.Lane);
        writer.WriteUInt16((ushort)message.Items.Length);
        for (int i = 0; i < message.Items.Length; i++)
        {
            writer.WriteInt32(message.Items[i].MailId);
            writer.WriteInt32(message.Items[i].PositionCm);
        }

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

    public static bool TryDecode(ReadOnlySpan<byte> payload, out LaneChecksum message)
    {
        message = null!;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.LaneChecksum)) return false;
        if (!reader.TryReadUInt64(out var segment)) return false;
        if (!TryReadLane(reader, out var lane)) return false;
        if (!reader.TryReadUInt16(out var count)) return false;
        if (!reader.TryReadUInt32(out var hash)) return false;
        if (!reader.AtEnd) return false;

        message = new LaneChecksum(new SegmentId(segment), lane, count, hash);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out LaneState message)
    {
        message = null!;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.LaneState)) return false;
        if (!reader.TryReadUInt64(out var segment)) return false;
        if (!TryReadLane(reader, out var lane)) return false;
        if (!reader.TryReadUInt16(out var count)) return false;

        var items = new LaneStateItem[count];
        for (int i = 0; i < count; i++)
        {
            if (!reader.TryReadInt32(out var mailId)) return false;
            if (!reader.TryReadInt32(out var positionCm)) return false;
            if (positionCm < 0) return false;
            items[i] = new LaneStateItem(mailId, positionCm);
        }

        if (!reader.AtEnd) return false;
        message = new LaneState(new SegmentId(segment), lane, items);
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
