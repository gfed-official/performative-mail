namespace PerformativeMail.Sim.Core;

public readonly record struct MailId(uint Value);

public readonly record struct AddressId(byte District, byte Street, byte Number, byte Unit)
{
    public uint Packed =>
        ((uint)District << 24) | ((uint)Street << 16) | ((uint)Number << 8) | Unit;

    public static AddressId Unpack(uint packed) => new(
        (byte)(packed >> 24),
        (byte)(packed >> 16),
        (byte)(packed >> 8),
        (byte)packed);
}

public readonly record struct SegmentId(ulong Value);

public readonly record struct AddressColour(byte District, byte Street)
{
    public static AddressColour From(AddressId address) => new(address.District, address.Street);
}

public readonly record struct MailKindId(ushort Value);

public readonly record struct DestinationId(uint Value);

public readonly record struct ItemDefId(ushort Value);

public readonly record struct ContainerId(uint Value);

public readonly record struct EntryId(uint Value)
{
    public static readonly EntryId None = new(0);

    public bool IsNone => Value == 0;
}

public readonly record struct ContainerVersion(uint Value)
{
    public ContainerVersion Next => new(Value + 1);
}
