using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Net.Tests.Soak;

public sealed class HashWitness
{
    public HashWitness(
        uint tick,
        ContainerId container,
        ContainerVersion version,
        ulong serverHash,
        IReadOnlyList<(ConnectionId Seat, ulong Hash)> viewerHashes)
    {
        if (viewerHashes is null)
            throw new ArgumentNullException(nameof(viewerHashes));

        var copy = new (ConnectionId Seat, ulong Hash)[viewerHashes.Count];
        for (int i = 0; i < viewerHashes.Count; i++)
            copy[i] = viewerHashes[i];

        Tick = tick;
        Container = container;
        Version = version;
        ServerHash = serverHash;
        ViewerHashes = copy;
    }

    public uint Tick { get; }

    public ContainerId Container { get; }

    public ContainerVersion Version { get; }

    public ulong ServerHash { get; }

    public IReadOnlyList<(ConnectionId Seat, ulong Hash)> ViewerHashes { get; }
}
