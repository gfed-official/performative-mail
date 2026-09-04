using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Net;

public readonly record struct FlattenedTile(int X, int Y, int H);

public readonly record struct ContainerStamp(ContainerId Id, ContainerVersion Version);

public readonly record struct WorldDeltas
{
    private readonly uint[] _depleted;
    private readonly FlattenedTile[] _flattened;
    private readonly uint[] _ruins;

    public WorldDeltas(
        IReadOnlyList<uint> depletedNodes,
        IReadOnlyList<FlattenedTile> flattenedTiles,
        IReadOnlyList<uint> ruins)
    {
        _depleted = CopyUInt32(depletedNodes, nameof(depletedNodes));
        _flattened = CopyTiles(flattenedTiles, nameof(flattenedTiles));
        _ruins = CopyUInt32(ruins, nameof(ruins));
    }

    public IReadOnlyList<uint> DepletedNodes => _depleted ?? Array.Empty<uint>();

    public IReadOnlyList<FlattenedTile> FlattenedTiles => _flattened ?? Array.Empty<FlattenedTile>();

    public IReadOnlyList<uint> Ruins => _ruins ?? Array.Empty<uint>();

    public static WorldDeltas Empty { get; } = new(
        Array.Empty<uint>(),
        Array.Empty<FlattenedTile>(),
        Array.Empty<uint>());

    public bool Equals(WorldDeltas other) =>
        EqualUInt32(DepletedNodes, other.DepletedNodes)
        && EqualTiles(FlattenedTiles, other.FlattenedTiles)
        && EqualUInt32(Ruins, other.Ruins);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        var depleted = DepletedNodes;
        for (int i = 0; i < depleted.Count; i++)
            hash.Add(depleted[i]);
        var tiles = FlattenedTiles;
        for (int i = 0; i < tiles.Count; i++)
            hash.Add(tiles[i]);
        var ruins = Ruins;
        for (int i = 0; i < ruins.Count; i++)
            hash.Add(ruins[i]);
        return hash.ToHashCode();
    }

    private static uint[] CopyUInt32(IReadOnlyList<uint> source, string paramName)
    {
        if (source is null)
            throw new ArgumentNullException(paramName);
        if (source.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(paramName, "Count must fit in a ushort.");
        if (source.Count == 0)
            return Array.Empty<uint>();

        var copy = new uint[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return copy;
    }

    private static FlattenedTile[] CopyTiles(IReadOnlyList<FlattenedTile> source, string paramName)
    {
        if (source is null)
            throw new ArgumentNullException(paramName);
        if (source.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(paramName, "Count must fit in a ushort.");
        if (source.Count == 0)
            return Array.Empty<FlattenedTile>();

        var copy = new FlattenedTile[source.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = source[i];
        return copy;
    }

    private static bool EqualUInt32(IReadOnlyList<uint> left, IReadOnlyList<uint> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private static bool EqualTiles(IReadOnlyList<FlattenedTile> left, IReadOnlyList<FlattenedTile> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }
}

public readonly record struct JoinState
{
    private readonly ContainerStamp[] _containers;

    public JoinState(
        uint seed,
        ulong worldHash,
        WorldDeltas deltas,
        RunState run,
        IReadOnlyList<ContainerStamp> containers)
    {
        Seed = seed;
        WorldHash = worldHash;
        Deltas = deltas;
        Run = run;
        _containers = CopyStamps(containers);
    }

    public uint Seed { get; }

    public ulong WorldHash { get; }

    public WorldDeltas Deltas { get; }

    public RunState Run { get; }

    public IReadOnlyList<ContainerStamp> Containers => _containers ?? Array.Empty<ContainerStamp>();

    public bool Equals(JoinState other)
    {
        if (Seed != other.Seed
            || WorldHash != other.WorldHash
            || Run != other.Run
            || !Deltas.Equals(other.Deltas))
            return false;

        var left = Containers;
        var right = other.Containers;
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Seed);
        hash.Add(WorldHash);
        hash.Add(Deltas);
        hash.Add(Run);
        var stamps = Containers;
        for (int i = 0; i < stamps.Count; i++)
            hash.Add(stamps[i]);
        return hash.ToHashCode();
    }

    private static ContainerStamp[] CopyStamps(IReadOnlyList<ContainerStamp> containers)
    {
        if (containers is null)
            throw new ArgumentNullException(nameof(containers));
        if (containers.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(containers), "Container count must fit in a ushort.");
        if (containers.Count == 0)
            return Array.Empty<ContainerStamp>();

        var copy = new ContainerStamp[containers.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = containers[i];
        return copy;
    }
}
