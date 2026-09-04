using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Sim.Run;

public readonly record struct RunSettings
{
    private readonly string[] _stamps;

    public RunSettings(
        uint seed,
        string archetype,
        IReadOnlyList<string> stamps,
        byte maxPlayers,
        LobbyVisibility visibility,
        string hostKit,
        uint protocolHash,
        uint contentHash)
    {
        if (archetype is null || !IsContentId(archetype))
            throw new ArgumentException("Archetype must be a lowercase snake_case content id.", nameof(archetype));
        if (hostKit is null || !IsContentId(hostKit))
            throw new ArgumentException("HostKit must be a lowercase snake_case content id.", nameof(hostKit));
        if (maxPlayers < 1 || maxPlayers > 8)
            throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, null);
        if ((byte)visibility is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null);

        Seed = seed;
        Archetype = archetype;
        _stamps = CopyStamps(stamps);
        MaxPlayers = maxPlayers;
        Visibility = visibility;
        HostKit = hostKit;
        ProtocolHash = protocolHash;
        ContentHash = contentHash;
    }

    public uint Seed { get; }

    public string Archetype { get; }

    public IReadOnlyList<string> Stamps => _stamps ?? Array.Empty<string>();

    public byte MaxPlayers { get; }

    public LobbyVisibility Visibility { get; }

    public string HostKit { get; }

    public uint ProtocolHash { get; }

    public uint ContentHash { get; }

    public static RunSettings Arcade() => new(
        seed: 0x7F3A9C21,
        archetype: "small_island",
        stamps: Array.Empty<string>(),
        maxPlayers: 8,
        visibility: LobbyVisibility.Friends,
        hostKit: "land",
        protocolHash: Protocol.SchemaHash,
        contentHash: Protocol.ContentHash);

    public bool Equals(RunSettings other)
    {
        if (Seed != other.Seed
            || MaxPlayers != other.MaxPlayers
            || Visibility != other.Visibility
            || ProtocolHash != other.ProtocolHash
            || ContentHash != other.ContentHash)
            return false;
        if (!string.Equals(Archetype, other.Archetype, StringComparison.Ordinal))
            return false;
        if (!string.Equals(HostKit, other.HostKit, StringComparison.Ordinal))
            return false;

        var left = Stamps;
        var right = other.Stamps;
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Seed);
        hash.Add(Archetype);
        var stamps = Stamps;
        for (int i = 0; i < stamps.Count; i++)
            hash.Add(stamps[i]);
        hash.Add(MaxPlayers);
        hash.Add(Visibility);
        hash.Add(HostKit);
        hash.Add(ProtocolHash);
        hash.Add(ContentHash);
        return hash.ToHashCode();
    }

    private static string[] CopyStamps(IReadOnlyList<string> stamps)
    {
        if (stamps is null)
            throw new ArgumentNullException(nameof(stamps));
        if (stamps.Count == 0)
            return Array.Empty<string>();

        var copy = new string[stamps.Count];
        for (int i = 0; i < stamps.Count; i++)
        {
            var id = stamps[i];
            if (id is null || !IsContentId(id))
                throw new ArgumentException("Stamps must be lowercase snake_case content ids.", nameof(stamps));
            copy[i] = id;
        }

        return copy;
    }

    private static bool IsContentId(string id)
    {
        if (id.Length == 0) return false;
        if (id[0] is < 'a' or > 'z') return false;
        for (int i = 1; i < id.Length; i++)
        {
            char c = id[i];
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_') continue;
            return false;
        }

        return true;
    }
}
