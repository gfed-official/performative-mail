namespace PerformativeMail.Sim.Core;

public readonly struct EntityId
{
    public uint Value { get; }

    public EntityId(uint value) => Value = value;

    public byte Class => ClassOf(Value);

    public uint Counter => CounterOf(Value);

    public static byte ClassOf(uint value) => (byte)(value >> 24);

    public static uint CounterOf(uint value) => value & 0x00FFFFFFu;

    public static EntityId FromClassAndCounter(byte entityClass, uint counter)
        => new(((uint)entityClass << 24) | (counter & 0x00FFFFFFu));
}
