using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Sim.Net;

public static class ConstructCodec
{
    public static byte[] Encode(in PlaceConstructRequest message)
    {
        if (message.BuildingId is null) throw new ArgumentNullException(nameof(message));

        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.PlaceConstruct);
        writer.WriteUInt32(message.ReqId);
        writer.WriteUtf8(message.BuildingId);
        writer.WriteInt32(message.TileX);
        writer.WriteInt32(message.TileY);
        writer.WriteByte((byte)message.Rotation);
        return writer.ToArray();
    }

    public static byte[] Encode(in PlaceConstructConfirmed message)
    {
        if (message.BuildingId is null) throw new ArgumentNullException(nameof(message));

        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.PlaceConstructConfirmed);
        writer.WriteUInt32(message.ReqId);
        writer.WriteUInt32(message.ConstructId.Value);
        writer.WriteUtf8(message.BuildingId);
        writer.WriteInt32(message.TileX);
        writer.WriteInt32(message.TileY);
        writer.WriteByte((byte)message.Rotation);
        writer.WriteUInt32(message.Owner.Value);
        return writer.ToArray();
    }

    public static byte[] Encode(in RemoveConstructRequest message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.RemoveConstruct);
        writer.WriteUInt32(message.ReqId);
        writer.WriteUInt32(message.ConstructId.Value);
        return writer.ToArray();
    }

    public static byte[] Encode(in RemoveConstructConfirmed message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.RemoveConstructConfirmed);
        writer.WriteUInt32(message.ReqId);
        writer.WriteUInt32(message.ConstructId.Value);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out PlaceConstructRequest message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.PlaceConstruct)) return false;
        if (!reader.TryReadUInt32(out var reqId)) return false;
        if (!reader.TryReadUtf8(out var buildingId)) return false;
        if (!reader.TryReadInt32(out var tileX)) return false;
        if (!reader.TryReadInt32(out var tileY)) return false;
        if (!TryReadFacing(reader, out var rotation)) return false;
        if (!reader.AtEnd) return false;

        message = new PlaceConstructRequest(reqId, buildingId, tileX, tileY, rotation);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out PlaceConstructConfirmed message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.PlaceConstructConfirmed)) return false;
        if (!reader.TryReadUInt32(out var reqId)) return false;
        if (!reader.TryReadUInt32(out var constructId)) return false;
        if (!reader.TryReadUtf8(out var buildingId)) return false;
        if (!reader.TryReadInt32(out var tileX)) return false;
        if (!reader.TryReadInt32(out var tileY)) return false;
        if (!TryReadFacing(reader, out var rotation)) return false;
        if (!reader.TryReadUInt32(out var owner)) return false;
        if (!reader.AtEnd) return false;

        message = new PlaceConstructConfirmed(
            reqId,
            new EntityId(constructId),
            buildingId,
            tileX,
            tileY,
            rotation,
            new EntityId(owner));
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out RemoveConstructRequest message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.RemoveConstruct)) return false;
        if (!reader.TryReadUInt32(out var reqId)) return false;
        if (!reader.TryReadUInt32(out var constructId)) return false;
        if (!reader.AtEnd) return false;

        message = new RemoveConstructRequest(reqId, new EntityId(constructId));
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out RemoveConstructConfirmed message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.RemoveConstructConfirmed)) return false;
        if (!reader.TryReadUInt32(out var reqId)) return false;
        if (!reader.TryReadUInt32(out var constructId)) return false;
        if (!reader.AtEnd) return false;

        message = new RemoveConstructConfirmed(reqId, new EntityId(constructId));
        return true;
    }

    private static bool TryReadKind(BitReader reader, MessageKind expected)
    {
        if (!reader.TryReadByte(out var raw)) return false;
        return raw == (byte)expected;
    }

    private static bool TryReadFacing(BitReader reader, out Facing facing)
    {
        facing = default;
        if (!reader.TryReadByte(out var raw)) return false;
        if (raw > (byte)Facing.West) return false;
        facing = (Facing)raw;
        return true;
    }
}
