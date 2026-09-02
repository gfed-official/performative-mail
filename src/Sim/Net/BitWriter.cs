using System;

namespace PerformativeMail.Sim.Net;

public sealed class BitWriter
{
    private byte[] _buffer = new byte[64];
    private int _length;

    public int Length => _length;

    public void WriteByte(byte value)
    {
        Ensure(1);
        _buffer[_length++] = value;
    }

    public void WriteSByte(sbyte value) => WriteByte(unchecked((byte)value));

    public void WriteUInt16(ushort value)
    {
        Ensure(2);
        _buffer[_length++] = (byte)value;
        _buffer[_length++] = (byte)(value >> 8);
    }

    public void WriteUInt32(uint value)
    {
        Ensure(4);
        _buffer[_length++] = (byte)value;
        _buffer[_length++] = (byte)(value >> 8);
        _buffer[_length++] = (byte)(value >> 16);
        _buffer[_length++] = (byte)(value >> 24);
    }

    public void WriteInt32(int value) => WriteUInt32(unchecked((uint)value));

    public void WriteUInt64(ulong value)
    {
        WriteUInt32(unchecked((uint)value));
        WriteUInt32(unchecked((uint)(value >> 32)));
    }

    public byte[] ToArray()
    {
        var copy = new byte[_length];
        Array.Copy(_buffer, copy, _length);
        return copy;
    }

    private void Ensure(int needed)
    {
        int required = _length + needed;
        if (required <= _buffer.Length) return;

        int capacity = _buffer.Length;
        while (capacity < required)
            capacity *= 2;
        var grown = new byte[capacity];
        Array.Copy(_buffer, grown, _length);
        _buffer = grown;
    }
}
