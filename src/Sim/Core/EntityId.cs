using System;

namespace PerformativeMail.Sim.Core;

public readonly struct EntityId : IEquatable<EntityId>
{
    public uint Value { get; }

    public EntityId(uint value) => Value = value;

    public byte Class => ClassOf(Value);

    public uint Counter => CounterOf(Value);

    public static byte ClassOf(uint value) => (byte)(value >> 24);

    public static uint CounterOf(uint value) => value & 0x00FFFFFFu;

    public static EntityId FromClassAndCounter(byte entityClass, uint counter)
        => new(((uint)entityClass << 24) | (counter & 0x00FFFFFFu));

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

    public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
}
