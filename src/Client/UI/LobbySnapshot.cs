using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct LobbySnapshot
{
    private readonly LobbyPlayer[] _players;

    public LobbySnapshot(in RunSettings settings, IReadOnlyList<LobbyPlayer> players)
    {
        Settings = settings;
        _players = Copy(players);
    }

    public RunSettings Settings { get; }

    public IReadOnlyList<LobbyPlayer> Players => _players ?? Array.Empty<LobbyPlayer>();

    private static LobbyPlayer[] Copy(IReadOnlyList<LobbyPlayer> players)
    {
        if (players is null)
            throw new ArgumentNullException(nameof(players));
        if (players.Count == 0)
            return Array.Empty<LobbyPlayer>();

        var copy = new LobbyPlayer[players.Count];
        for (int i = 0; i < players.Count; i++)
            copy[i] = players[i];
        return copy;
    }
}
