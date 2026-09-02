using PerformativeMail.Sim.Net;

namespace PerformativeMail.Client;

public abstract class PlayerReplication
{
    private PlayerReplication()
    {
    }

    public sealed class OwnerPredicted : PlayerReplication
    {
        public OwnerPredicted(PredictionState state)
        {
            State = state;
        }

        public PredictionState State { get; }
    }

    public sealed class RemoteInterpolated : PlayerReplication
    {
        public RemoteInterpolated(InterpolationBuffer buffer)
        {
            Buffer = buffer;
        }

        public InterpolationBuffer Buffer { get; }
    }
}
