using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests.Soak;

public abstract record HashVerdict
{
    private HashVerdict()
    {
    }

    public sealed record Match : HashVerdict
    {
        public static readonly Match Instance = new();

        private Match()
        {
        }
    }

    public sealed record MissingViewer(ConnectionId Seat) : HashVerdict;

    public sealed record HashMismatch(ConnectionId Seat, ulong Expected, ulong Actual) : HashVerdict;

    public sealed record VersionGap(ConnectionId Seat) : HashVerdict;
}
