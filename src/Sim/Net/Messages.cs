using System;
using System.Collections.Generic;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;

namespace PerformativeMail.Sim.Net;

public readonly record struct Hello(uint ProtocolHash);

public readonly record struct HelloOk(EntityId LocalPlayer, uint StartTick);

public enum HelloRejectReason : byte
{
    ProtocolMismatch = 1,
    VersionMismatch = 2,
}

public readonly record struct HelloReject(HelloRejectReason Reason);

public readonly record struct WorldOffer(uint Seed, ulong WorldHash);

public readonly record struct Ping(uint ClientStamp);

public readonly record struct Pong(uint ClientStamp, uint ServerTick);

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

public readonly record struct OwnerSnapshot(
    uint ServerTick,
    PlayerPose Pose,
    uint LastProcessedInputTick)
{
    public static bool TryFrom(SnapshotPacket packet, EntityId owner, out OwnerSnapshot snapshot)
    {
        snapshot = default;
        if (packet is null)
            return false;

        for (int i = 0; i < packet.Players.Count; i++)
        {
            var player = packet.Players[i];
            if (player.Id != owner)
                continue;

            snapshot = new OwnerSnapshot(
                packet.ServerTick,
                new PlayerPose(player.Xcm, player.Ycm, player.Zcm, player.Yaw),
                player.LastProcessedInputTick);
            return true;
        }

        return false;
    }
}

public readonly record struct RemoteSnapshot(
    uint ServerTick,
    EntityId Id,
    PlayerPose Pose)
{
    public TimeSpan ServerTime => InterpolationBuffer.TimeOfTick(ServerTick);

    public static RemoteSnapshot From(in PlayerSnapshot player, uint serverTick, EntityId owner)
    {
        if (player.Id == owner)
            throw new InvalidOperationException("Owner pawn must not enter InterpolationBuffer.");

        return new RemoteSnapshot(
            serverTick,
            player.Id,
            new PlayerPose(player.Xcm, player.Ycm, player.Zcm, player.Yaw));
    }
}

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
