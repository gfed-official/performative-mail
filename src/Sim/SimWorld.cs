using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Players;

namespace PerformativeMail.Sim;

public sealed class SimWorld
{
    public uint CurrentTick { get; private set; }

    public PlayerTable Players { get; } = new PlayerTable();

    public void Tick(uint tick)
    {
        CurrentTick = tick;
    }

    public void ApplyInput(EntityId sender, in InputCmd cmd)
    {
        if (!Players.TryGet(sender, out var body))
            return;

        body.Apply(in cmd);
    }

    public void ApplyRequest(byte[] payload)
    {
    }
}
