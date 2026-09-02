using System;
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
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        public PredictionState State { get; }
    }

    public sealed class RemoteInterpolated : PlayerReplication
    {
        public RemoteInterpolated(InterpolationBuffer buffer)
        {
            Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public InterpolationBuffer Buffer { get; }
    }
}
