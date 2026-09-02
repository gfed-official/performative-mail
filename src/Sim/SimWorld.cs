namespace PerformativeMail.Sim;

public sealed class SimWorld
{
    public uint CurrentTick { get; private set; }

    public void Tick(uint tick)
    {
        CurrentTick = tick;
    }

    public void ApplyInput(uint tick, byte[] payload)
    {
    }

    public void ApplyRequest(byte[] payload)
    {
    }
}
