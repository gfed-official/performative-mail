using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Players;

public sealed class PlayerBody
{
    private uint? _lastProcessed;

    public PlayerBody(EntityId id)
    {
        Id = id;
        HpPct = 100;
    }

    public EntityId Id { get; }

    public int Xcm { get; }

    public int Ycm { get; }

    public int Zcm { get; }

    public ushort Yaw { get; }

    public byte Anim { get; }

    public byte HpPct { get; }

    public uint LastProcessedInputTick => _lastProcessed ?? 0;

    public InputCmd LastCmd { get; private set; }

    public uint AppliedCount { get; private set; }

    public bool HasAppliedInput => _lastProcessed.HasValue;

    public void Apply(in InputCmd cmd)
    {
        LastCmd = cmd;
        _lastProcessed = cmd.Tick;
        AppliedCount++;
    }
}
