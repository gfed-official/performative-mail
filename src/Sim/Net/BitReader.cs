using System;

namespace PerformativeMail.Sim.Net;

public sealed class BitReader
{
    private readonly byte[] _data;
    private int _offset;

    public BitReader(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public BitReader(ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
    }

    public int Remaining => _data.Length - _offset;

    public bool AtEnd => _offset == _data.Length;

    public bool TryReadByte(out byte value)
    {
        if (_offset >= _data.Length)
        {
            value = 0;
            return false;
        }

        value = _data[_offset++];
        return true;
    }

    public bool TryReadSByte(out sbyte value)
    {
        if (!TryReadByte(out var raw))
        {
            value = 0;
            return false;
        }

        value = unchecked((sbyte)raw);
        return true;
    }

    public bool TryReadUInt16(out ushort value)
    {
        if (_offset + 2 > _data.Length)
        {
            value = 0;
            return false;
        }

        value = (ushort)(_data[_offset] | (_data[_offset + 1] << 8));
        _offset += 2;
        return true;
    }

    public bool TryReadUInt32(out uint value)
    {
        if (_offset + 4 > _data.Length)
        {
            value = 0;
            return false;
        }

        value = (uint)(_data[_offset]
            | (_data[_offset + 1] << 8)
            | (_data[_offset + 2] << 16)
            | (_data[_offset + 3] << 24));
        _offset += 4;
        return true;
    }

    public bool TryReadInt32(out int value)
    {
        if (!TryReadUInt32(out var raw))
        {
            value = 0;
            return false;
        }

        value = unchecked((int)raw);
        return true;
    }
}
