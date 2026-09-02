using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;

namespace PerformativeMail.Sim.Net;

public static class InventoryCodec
{
    private enum ChangeKind : byte
    {
        Upsert = 1,
        Remove = 2,
        Reset = 3,
    }

    private enum StackKind : byte
    {
        Mail = 1,
        Item = 2,
    }

    public static byte[] EncodeEvent(ContainerDelta delta, uint? reqId = null)
    {
        if (delta is null) throw new ArgumentNullException(nameof(delta));
        if (delta.Changes is null) throw new ArgumentNullException(nameof(delta.Changes));
        if (delta.Changes.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(delta), "Change count must fit in a ushort.");

        var writer = new BitWriter();
        writer.WriteByte((byte)MessageKind.InventoryEvent);
        writer.WriteUInt32(delta.Container.Value);
        writer.WriteUInt32(delta.BeforeVersion.Value);
        writer.WriteUInt32(delta.Version.Value);
        writer.WriteUInt64(delta.Hash);
        writer.WriteUInt16((ushort)delta.Changes.Count);
        for (int i = 0; i < delta.Changes.Count; i++)
            WriteChange(writer, delta.Changes[i]);

        if (reqId is uint id)
        {
            writer.WriteByte(1);
            writer.WriteUInt32(id);
        }
        else
        {
            writer.WriteByte(0);
        }

        return writer.ToArray();
    }

    public static bool TryParseEvent(ReadOnlySpan<byte> payload, out ContainerDelta delta, out uint? reqId)
    {
        delta = null!;
        reqId = null;
        var reader = new BitReader(payload);
        if (!reader.TryReadByte(out var kind) || kind != (byte)MessageKind.InventoryEvent)
            return false;
        if (!reader.TryReadUInt32(out var container))
            return false;
        if (!reader.TryReadUInt32(out var before))
            return false;
        if (!reader.TryReadUInt32(out var version))
            return false;
        if (!reader.TryReadUInt64(out var hash))
            return false;
        if (!reader.TryReadUInt16(out var changeCount))
            return false;

        var changes = new Change[changeCount];
        for (int i = 0; i < changeCount; i++)
        {
            if (!TryReadChange(reader, out changes[i]))
                return false;
        }

        if (!reader.TryReadByte(out var hasReqId))
            return false;
        if (hasReqId == 1)
        {
            if (!reader.TryReadUInt32(out var id))
                return false;
            reqId = id;
        }
        else if (hasReqId != 0)
        {
            return false;
        }

        if (!reader.AtEnd)
            return false;

        delta = new ContainerDelta(
            new ContainerId(container),
            new ContainerVersion(before),
            new ContainerVersion(version),
            hash,
            changes);
        return true;
    }

    private static void WriteChange(BitWriter writer, Change change)
    {
        switch (change)
        {
            case Upsert upsert:
                writer.WriteByte((byte)ChangeKind.Upsert);
                WriteEntry(writer, upsert.Entry);
                break;
            case Remove remove:
                writer.WriteByte((byte)ChangeKind.Remove);
                writer.WriteUInt32(remove.Id.Value);
                break;
            case Reset reset:
                writer.WriteByte((byte)ChangeKind.Reset);
                WriteSpec(writer, reset.Spec);
                if (reset.Entries.Count > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(change), "Reset entry count must fit in a ushort.");
                writer.WriteUInt16((ushort)reset.Entries.Count);
                for (int i = 0; i < reset.Entries.Count; i++)
                    WriteEntry(writer, reset.Entries[i]);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, null);
        }
    }

    private static bool TryReadChange(BitReader reader, out Change change)
    {
        change = null!;
        if (!reader.TryReadByte(out var raw))
            return false;

        switch ((ChangeKind)raw)
        {
            case ChangeKind.Upsert:
                if (!TryReadEntry(reader, out var upserted))
                    return false;
                change = new Upsert(upserted);
                return true;
            case ChangeKind.Remove:
                if (!reader.TryReadUInt32(out var entryId))
                    return false;
                change = new Remove(new EntryId(entryId));
                return true;
            case ChangeKind.Reset:
                if (!TryReadSpec(reader, out var spec))
                    return false;
                if (!reader.TryReadUInt16(out var count))
                    return false;
                var entries = new Entry[count];
                for (int i = 0; i < count; i++)
                {
                    if (!TryReadEntry(reader, out entries[i]))
                        return false;
                }

                change = new Reset(spec, entries);
                return true;
            default:
                return false;
        }
    }

    private static void WriteEntry(BitWriter writer, in Entry entry)
    {
        writer.WriteUInt32(entry.Id.Value);
        writer.WriteByte(entry.At.X);
        writer.WriteByte(entry.At.Y);
        writer.WriteByte(entry.At.Rotated ? (byte)1 : (byte)0);
        WriteStack(writer, entry.Stack);
    }

    private static bool TryReadEntry(BitReader reader, out Entry entry)
    {
        entry = default;
        if (!reader.TryReadUInt32(out var id))
            return false;
        if (!reader.TryReadByte(out var x))
            return false;
        if (!reader.TryReadByte(out var y))
            return false;
        if (!reader.TryReadByte(out var rotated))
            return false;
        if (!TryReadStack(reader, out var stack))
            return false;

        entry = new Entry(new EntryId(id), stack, new Placement(x, y, rotated != 0));
        return true;
    }

    private static void WriteStack(BitWriter writer, Stack stack)
    {
        switch (stack)
        {
            case MailStack mail:
                if (mail.Ids.Count > ushort.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(stack), "Mail id count must fit in a ushort.");
                writer.WriteByte((byte)StackKind.Mail);
                writer.WriteUInt16(mail.Kind.Value);
                writer.WriteUInt32(mail.Address.Packed);
                writer.WriteUInt16((ushort)mail.Ids.Count);
                for (int i = 0; i < mail.Ids.Count; i++)
                    writer.WriteUInt32(mail.Ids[i].Value);
                break;
            case ItemStack item:
                writer.WriteByte((byte)StackKind.Item);
                writer.WriteUInt16(item.Item.Value);
                writer.WriteInt32(item.Count);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stack), stack, null);
        }
    }

    private static bool TryReadStack(BitReader reader, out Stack stack)
    {
        stack = null!;
        if (!reader.TryReadByte(out var raw))
            return false;

        switch ((StackKind)raw)
        {
            case StackKind.Mail:
                if (!reader.TryReadUInt16(out var kind))
                    return false;
                if (!reader.TryReadUInt32(out var address))
                    return false;
                if (!reader.TryReadUInt16(out var count))
                    return false;
                if (count < 1)
                    return false;
                var ids = new MailId[count];
                for (int i = 0; i < count; i++)
                {
                    if (!reader.TryReadUInt32(out var mailId))
                        return false;
                    ids[i] = new MailId(mailId);
                }

                stack = new MailStack(new MailKindId(kind), AddressId.Unpack(address), ids);
                return true;
            case StackKind.Item:
                if (!reader.TryReadUInt16(out var def))
                    return false;
                if (!reader.TryReadInt32(out var itemCount) || itemCount < 1)
                    return false;
                stack = new ItemStack(new ItemDefId(def), itemCount);
                return true;
            default:
                return false;
        }
    }

    private static void WriteSpec(BitWriter writer, ContainerSpec spec)
    {
        var shape = spec.Shape;
        writer.WriteByte(shape.Cols);
        writer.WriteByte(shape.Rows);
        writer.WriteByte(shape.IgnoresFootprint ? (byte)1 : (byte)0);

        var blocked = BlockedCells(in shape);
        writer.WriteUInt16((ushort)blocked.Count);
        for (int i = 0; i < blocked.Count; i++)
        {
            writer.WriteByte(blocked[i].X);
            writer.WriteByte(blocked[i].Y);
        }

        if (spec.AllowedCategories is null)
        {
            writer.WriteByte(0);
            return;
        }

        writer.WriteByte(1);
        writer.WriteByte((byte)spec.AllowedCategories.Count);
        foreach (var category in spec.AllowedCategories)
            writer.WriteByte((byte)category);
    }

    private static bool TryReadSpec(BitReader reader, out ContainerSpec spec)
    {
        spec = null!;
        if (!reader.TryReadByte(out var cols))
            return false;
        if (!reader.TryReadByte(out var rows))
            return false;
        if (!reader.TryReadByte(out var ignores))
            return false;
        if (!reader.TryReadUInt16(out var blockedCount))
            return false;

        var blocked = new Cell[blockedCount];
        for (int i = 0; i < blockedCount; i++)
        {
            if (!reader.TryReadByte(out var x) || !reader.TryReadByte(out var y))
                return false;
            blocked[i] = new Cell(x, y);
        }

        ContainerShape shape;
        if (ignores != 0)
            shape = ContainerShape.Slot;
        else
            shape = ContainerShape.Grid(cols, rows, blocked);

        if (!reader.TryReadByte(out var allowMode))
            return false;

        IReadOnlyCollection<StackCategory>? allowed = null;
        if (allowMode == 1)
        {
            if (!reader.TryReadByte(out var allowCount))
                return false;
            var categories = new StackCategory[allowCount];
            for (int i = 0; i < allowCount; i++)
            {
                if (!reader.TryReadByte(out var category))
                    return false;
                categories[i] = (StackCategory)category;
            }

            allowed = categories;
        }
        else if (allowMode != 0)
        {
            return false;
        }

        spec = new ContainerSpec(shape, allowed);
        return true;
    }

    private static List<Cell> BlockedCells(in ContainerShape shape)
    {
        var blocked = new List<Cell>();
        for (byte y = 0; y < shape.Rows; y++)
        {
            for (byte x = 0; x < shape.Cols; x++)
            {
                var cell = new Cell(x, y);
                if (shape.IsBlocked(cell))
                    blocked.Add(cell);
            }
        }

        return blocked;
    }
}
