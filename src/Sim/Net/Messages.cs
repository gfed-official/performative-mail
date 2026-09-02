using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;

namespace PerformativeMail.Sim.Net;

public readonly record struct Hello(uint ProtocolHash);

public readonly record struct HelloOk(EntityId LocalPlayer, uint StartTick);

public enum HelloRejectReason : byte
{
    ProtocolMismatch = 1,
}

public readonly record struct HelloReject(HelloRejectReason Reason);

public sealed class InputPacket
{
    public IReadOnlyList<InputCmd> Commands { get; }

    public InputPacket(IReadOnlyList<InputCmd> commands)
    {
        if (commands is null) throw new ArgumentNullException(nameof(commands));
        if (commands.Count < 1 || commands.Count > 3)
            throw new ArgumentOutOfRangeException(nameof(commands), "InputPacket holds 1 to 3 commands.");

        var copy = new InputCmd[commands.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = commands[i];
        Commands = copy;
    }
}

public readonly record struct PlayerSnapshot(
    EntityId Id,
    int Xcm,
    int Ycm,
    int Zcm,
    ushort Yaw,
    byte Anim,
    byte HpPct,
    uint LastProcessedInputTick);

public sealed class SnapshotPacket
{
    public uint ServerTick { get; }

    public IReadOnlyList<PlayerSnapshot> Players { get; }

    public SnapshotPacket(uint serverTick, IReadOnlyList<PlayerSnapshot> players)
    {
        if (players is null) throw new ArgumentNullException(nameof(players));
        if (players.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(players), "SnapshotPacket player count must fit in a ushort.");

        ServerTick = serverTick;
        var copy = new PlayerSnapshot[players.Count];
        for (int i = 0; i < copy.Length; i++)
            copy[i] = players[i];
        Players = copy;
    }
}
