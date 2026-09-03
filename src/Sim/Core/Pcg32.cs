namespace PerformativeMail.Sim.Core;

public struct Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _inc;

    public Pcg32(ulong state, ulong seq)
    {
        _inc = (seq << 1) | 1UL;
        _state = 0;
        NextUInt32();
        _state += state;
        NextUInt32();
    }

    public uint NextUInt32()
    {
        ulong old = _state;
        _state = old * Multiplier + _inc;
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }
}
