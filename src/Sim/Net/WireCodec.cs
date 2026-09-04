using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Net;

public static class WireCodec
{
    public static bool TryPeekKind(ReadOnlySpan<byte> payload, out MessageKind kind)
    {
        if (payload.Length < 1)
        {
            kind = default;
            return false;
        }

        kind = (MessageKind)payload[0];
        return true;
    }

    public static byte[] Encode(in Hello message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.Hello);
        writer.WriteUInt32(message.ProtocolHash);
        return writer.ToArray();
    }

    public static byte[] Encode(in HelloOk message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.HelloOk);
        writer.WriteUInt32(message.LocalPlayer.Value);
        writer.WriteUInt32(message.StartTick);
        return writer.ToArray();
    }

    public static byte[] Encode(in HelloReject message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.HelloReject);
        writer.WriteByte((byte)message.Reason);
        return writer.ToArray();
    }

    public static byte[] Encode(InputPacket packet)
    {
        if (packet is null) throw new ArgumentNullException(nameof(packet));

        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.Input);
        writer.WriteByte((byte)packet.Commands.Count);
        for (int i = 0; i < packet.Commands.Count; i++)
            WriteCommand(writer, packet.Commands[i]);
        return writer.ToArray();
    }

    public static byte[] Encode(SnapshotPacket packet)
    {
        if (packet is null) throw new ArgumentNullException(nameof(packet));

        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.Snapshot);
        writer.WriteUInt32(packet.ServerTick);
        writer.WriteUInt16((ushort)packet.Players.Count);
        for (int i = 0; i < packet.Players.Count; i++)
            WritePlayer(writer, packet.Players[i]);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out Hello message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.Hello)) return false;
        if (!reader.TryReadUInt32(out var protocolHash)) return false;
        if (!reader.AtEnd) return false;
        message = new Hello(protocolHash);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out HelloOk message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.HelloOk)) return false;
        if (!reader.TryReadUInt32(out var localPlayer)) return false;
        if (!reader.TryReadUInt32(out var startTick)) return false;
        if (!reader.AtEnd) return false;
        message = new HelloOk(new EntityId(localPlayer), startTick);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out HelloReject message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.HelloReject)) return false;
        if (!reader.TryReadByte(out var reason)) return false;
        if (!reader.AtEnd) return false;
        message = new HelloReject((HelloRejectReason)reason);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out InputPacket? packet)
    {
        packet = null;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.Input)) return false;
        if (!reader.TryReadByte(out var count)) return false;
        if (count < 1 || count > 3) return false;

        var commands = new InputCmd[count];
        for (int i = 0; i < count; i++)
        {
            if (!TryReadCommand(reader, out commands[i])) return false;
        }

        if (!reader.AtEnd) return false;
        packet = new InputPacket(commands);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out SnapshotPacket? packet)
    {
        packet = null;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.Snapshot)) return false;
        if (!reader.TryReadUInt32(out var serverTick)) return false;
        if (!reader.TryReadUInt16(out var playerCount)) return false;

        var players = new PlayerSnapshot[playerCount];
        for (int i = 0; i < playerCount; i++)
        {
            if (!TryReadPlayer(reader, out players[i])) return false;
        }

        if (!reader.AtEnd) return false;
        packet = new SnapshotPacket(serverTick, players);
        return true;
    }

    public static byte[] Encode(in Ping message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.Ping);
        writer.WriteUInt32(message.ClientStamp);
        return writer.ToArray();
    }

    public static byte[] Encode(in Pong message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.Pong);
        writer.WriteUInt32(message.ClientStamp);
        writer.WriteUInt32(message.ServerTick);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out Ping message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.Ping)) return false;
        if (!reader.TryReadUInt32(out var stamp)) return false;
        if (!reader.AtEnd) return false;
        message = new Ping(stamp);
        return true;
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out Pong message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.Pong)) return false;
        if (!reader.TryReadUInt32(out var stamp)) return false;
        if (!reader.TryReadUInt32(out var serverTick)) return false;
        if (!reader.AtEnd) return false;
        message = new Pong(stamp, serverTick);
        return true;
    }

    public static byte[] Encode(in WorldOffer message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.WorldOffer);
        writer.WriteUInt32(message.Seed);
        writer.WriteUInt64(message.WorldHash);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out WorldOffer message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.WorldOffer)) return false;
        if (!reader.TryReadUInt32(out var seed)) return false;
        if (!reader.TryReadUInt64(out var worldHash)) return false;
        if (!reader.AtEnd) return false;
        message = new WorldOffer(seed, worldHash);
        return true;
    }

    public static byte[] Encode(in RunSettings settings)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.RunSettings);
        writer.WriteUInt32(settings.Seed);
        writer.WriteUtf8(settings.Archetype);
        writer.WriteByte((byte)settings.Stamps.Count);
        for (int i = 0; i < settings.Stamps.Count; i++)
            writer.WriteUtf8(settings.Stamps[i]);
        writer.WriteByte(settings.MaxPlayers);
        writer.WriteByte((byte)settings.Visibility);
        writer.WriteUtf8(settings.HostKit);
        writer.WriteUInt32(settings.ProtocolHash);
        writer.WriteUInt32(settings.ContentHash);
        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out RunSettings settings)
    {
        settings = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.RunSettings)) return false;
        if (!reader.TryReadUInt32(out var seed)) return false;
        if (!reader.TryReadUtf8(out var archetype) || archetype.Length == 0) return false;
        if (!reader.TryReadByte(out var stampCount)) return false;
        var stamps = stampCount == 0 ? Array.Empty<string>() : new string[stampCount];
        for (int i = 0; i < stampCount; i++)
        {
            if (!reader.TryReadUtf8(out stamps[i])) return false;
        }

        if (!reader.TryReadByte(out var maxPlayers)) return false;
        if (!reader.TryReadByte(out var visibility)) return false;
        if (!reader.TryReadUtf8(out var hostKit) || hostKit.Length == 0) return false;
        if (!reader.TryReadUInt32(out var protocolHash)) return false;
        if (!reader.TryReadUInt32(out var contentHash)) return false;
        if (!reader.AtEnd) return false;

        try
        {
            settings = new RunSettings(
                seed,
                archetype,
                stamps,
                maxPlayers,
                (LobbyVisibility)visibility,
                hostKit,
                protocolHash,
                contentHash);
            return true;
        }
        catch (ArgumentException)
        {
            settings = default;
            return false;
        }
    }

    public static byte[] Encode(in JoinState message)
    {
        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.JoinState);
        writer.WriteUInt32(message.Seed);
        writer.WriteUInt64(message.WorldHash);
        writer.WriteByte((byte)message.Run.Phase);
        writer.WriteByte(message.Run.Shift);
        writer.WriteUInt32(message.Run.PhaseDeadlineTick);

        var depleted = message.Deltas.DepletedNodes;
        writer.WriteUInt16((ushort)depleted.Count);
        for (int i = 0; i < depleted.Count; i++)
            writer.WriteUInt32(depleted[i]);

        var tiles = message.Deltas.FlattenedTiles;
        writer.WriteUInt16((ushort)tiles.Count);
        for (int i = 0; i < tiles.Count; i++)
        {
            writer.WriteInt32(tiles[i].X);
            writer.WriteInt32(tiles[i].Y);
            writer.WriteInt32(tiles[i].H);
        }

        var ruins = message.Deltas.Ruins;
        writer.WriteUInt16((ushort)ruins.Count);
        for (int i = 0; i < ruins.Count; i++)
            writer.WriteUInt32(ruins[i]);

        var stamps = message.Containers;
        writer.WriteUInt16((ushort)stamps.Count);
        for (int i = 0; i < stamps.Count; i++)
        {
            writer.WriteUInt32(stamps[i].Id.Value);
            writer.WriteUInt32(stamps[i].Version.Value);
        }

        return writer.ToArray();
    }

    public static bool TryDecode(ReadOnlySpan<byte> payload, out JoinState message)
    {
        message = default;
        var reader = new BitReader(payload);
        if (!TryReadKind(reader, MessageKind.JoinState)) return false;
        if (!reader.TryReadUInt32(out var seed)) return false;
        if (!reader.TryReadUInt64(out var worldHash)) return false;
        if (!reader.TryReadByte(out var phase)) return false;
        if (!reader.TryReadByte(out var shift)) return false;
        if (!reader.TryReadUInt32(out var deadline)) return false;
        if (!TryReadUInt32List(reader, out var depleted)) return false;
        if (!TryReadTiles(reader, out var tiles)) return false;
        if (!TryReadUInt32List(reader, out var ruins)) return false;
        if (!TryReadStamps(reader, out var stamps)) return false;
        if (!reader.AtEnd) return false;
        if (phase > (byte)RunPhase.Victory) return false;

        try
        {
            message = new JoinState(
                seed,
                worldHash,
                new WorldDeltas(depleted, tiles, ruins),
                new RunState((RunPhase)phase, shift, deadline),
                stamps);
            return true;
        }
        catch (ArgumentException)
        {
            message = default;
            return false;
        }
    }

    private static bool TryReadUInt32List(BitReader reader, out uint[] values)
    {
        values = Array.Empty<uint>();
        if (!reader.TryReadUInt16(out var count)) return false;
        if (count == 0) return true;

        values = new uint[count];
        for (int i = 0; i < count; i++)
        {
            if (!reader.TryReadUInt32(out values[i])) return false;
        }

        return true;
    }

    private static bool TryReadTiles(BitReader reader, out FlattenedTile[] tiles)
    {
        tiles = Array.Empty<FlattenedTile>();
        if (!reader.TryReadUInt16(out var count)) return false;
        if (count == 0) return true;

        tiles = new FlattenedTile[count];
        for (int i = 0; i < count; i++)
        {
            if (!reader.TryReadInt32(out var x)) return false;
            if (!reader.TryReadInt32(out var y)) return false;
            if (!reader.TryReadInt32(out var h)) return false;
            tiles[i] = new FlattenedTile(x, y, h);
        }

        return true;
    }

    private static bool TryReadStamps(BitReader reader, out ContainerStamp[] stamps)
    {
        stamps = Array.Empty<ContainerStamp>();
        if (!reader.TryReadUInt16(out var count)) return false;
        if (count == 0) return true;

        stamps = new ContainerStamp[count];
        for (int i = 0; i < count; i++)
        {
            if (!reader.TryReadUInt32(out var id)) return false;
            if (!reader.TryReadUInt32(out var version)) return false;
            stamps[i] = new ContainerStamp(new ContainerId(id), new ContainerVersion(version));
        }

        return true;
    }

    private static void WriteCommand(BitWriter writer, in InputCmd command)
    {
        writer.WriteUInt32(command.Tick);
        writer.WriteSByte(command.AxisX);
        writer.WriteSByte(command.AxisY);
        writer.WriteUInt16(command.Yaw);
        writer.WriteUInt16((ushort)command.Buttons);
    }

    private static void WritePlayer(BitWriter writer, in PlayerSnapshot player)
    {
        writer.WriteUInt32(player.Id.Value);
        writer.WriteInt32(player.Xcm);
        writer.WriteInt32(player.Ycm);
        writer.WriteInt32(player.Zcm);
        writer.WriteUInt16(player.Yaw);
        writer.WriteByte(player.Anim);
        writer.WriteByte(player.HpPct);
        writer.WriteUInt32(player.LastProcessedInputTick);
        if (player.VehicleId.Value != 0)
            writer.WriteUInt32(player.VehicleId.Value);
    }

    private static bool TryReadKind(BitReader reader, MessageKind expected)
    {
        if (!reader.TryReadByte(out var raw)) return false;
        return raw == (byte)expected;
    }

    private static bool TryReadCommand(BitReader reader, out InputCmd command)
    {
        command = default;
        if (!reader.TryReadUInt32(out var tick)) return false;
        if (!reader.TryReadSByte(out var axisX)) return false;
        if (!reader.TryReadSByte(out var axisY)) return false;
        if (!reader.TryReadUInt16(out var yaw)) return false;
        if (!reader.TryReadUInt16(out var buttons)) return false;
        command = new InputCmd(tick, axisX, axisY, yaw, (InputButtons)buttons);
        return true;
    }

    private static bool TryReadPlayer(BitReader reader, out PlayerSnapshot player)
    {
        player = default;
        if (!reader.TryReadUInt32(out var id)) return false;
        if (!reader.TryReadInt32(out var xcm)) return false;
        if (!reader.TryReadInt32(out var ycm)) return false;
        if (!reader.TryReadInt32(out var zcm)) return false;
        if (!reader.TryReadUInt16(out var yaw)) return false;
        if (!reader.TryReadByte(out var anim)) return false;
        if (!reader.TryReadByte(out var hpPct)) return false;
        if (!reader.TryReadUInt32(out var lastProcessed)) return false;
        var vehicleId = default(EntityId);
        if (!reader.AtEnd)
        {
            if (!reader.TryPeekUInt32(out var raw))
                return false;
            if (EntityId.ClassOf(raw) == EntityClass.Vehicle)
            {
                if (!reader.TryReadUInt32(out raw))
                    return false;
                vehicleId = new EntityId(raw);
            }
        }

        player = new PlayerSnapshot(new EntityId(id), xcm, ycm, zcm, yaw, anim, hpPct, lastProcessed, vehicleId);
        return true;
    }
}
