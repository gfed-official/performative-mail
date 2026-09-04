using System;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Net.Tests;

public sealed class WireCodecTests
{
    private static readonly EntityId FirstPlayer = EntityId.FromClassAndCounter(EntityClass.Player, 1);

    private static readonly byte[] HelloBytes =
    {
        0x01, 0xFA, 0xC9, 0x12, 0x41,
    };

    private static readonly byte[] HelloOkBytes =
    {
        0x02,
        0x01, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] HelloRejectBytes =
    {
        0x03, 0x01,
    };

    private static readonly byte[] HelloRejectVersionBytes =
    {
        0x03, 0x02,
    };

    private static readonly byte[] WorldOfferBytes =
    {
        0x32,
        0x21, 0x9C, 0x3A, 0x7F,
        0x0E, 0x68, 0x73, 0x48,
        0x05, 0x70, 0x16, 0x82,
    };

    private static readonly byte[] RunSettingsArcadeBytes =
    {
        0x33,
        0x21, 0x9C, 0x3A, 0x7F,
        0x0C,
        0x73, 0x6D, 0x61, 0x6C, 0x6C, 0x5F, 0x69, 0x73, 0x6C, 0x61, 0x6E, 0x64,
        0x00,
        0x08,
        0x02,
        0x04,
        0x6C, 0x61, 0x6E, 0x64,
        0xFA, 0xC9, 0x12, 0x41,
        0x00, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] JoinStateEmptyPrepBytes =
    {
        0x34,
        0x21, 0x9C, 0x3A, 0x7F,
        0x0E, 0x68, 0x73, 0x48,
        0x05, 0x70, 0x16, 0x82,
        0x02,
        0x03,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
        0x00, 0x00,
        0x00, 0x00,
        0x00, 0x00,
    };

    private static readonly byte[] RunSettingsCustomBytes =
    {
        0x33,
        0xD2, 0xEB, 0x3A, 0x7F,
        0x0C,
        0x73, 0x6D, 0x61, 0x6C, 0x6C, 0x5F, 0x69, 0x73, 0x6C, 0x61, 0x6E, 0x64,
        0x01,
        0x0C,
        0x64, 0x6F, 0x75, 0x62, 0x6C, 0x65, 0x5F, 0x72, 0x61, 0x69, 0x64, 0x73,
        0x04,
        0x03,
        0x04,
        0x6C, 0x61, 0x6E, 0x64,
        0xFA, 0xC9, 0x12, 0x41,
        0x00, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] InputOneBytes =
    {
        0x0A, 0x01,
        0x07, 0x00, 0x00, 0x00,
        0x81, 0x7F,
        0x00, 0x80,
        0x05, 0x00,
    };

    private static readonly byte[] InputThreeBytes =
    {
        0x0A, 0x03,
        0x0A, 0x00, 0x00, 0x00, 0x01, 0x00, 0x64, 0x00, 0x01, 0x00,
        0x09, 0x00, 0x00, 0x00, 0x00, 0xFF, 0xC8, 0x00, 0x02, 0x00,
        0x08, 0x00, 0x00, 0x00, 0xFF, 0x01, 0x2C, 0x01, 0x00, 0x00,
    };

    private static readonly byte[] SnapshotOneBytes =
    {
        0x14,
        0x1D, 0x00, 0x00, 0x00,
        0x01, 0x00,
        0x01, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
        0x00,
        0x64,
        0x1D, 0x00, 0x00, 0x00,
    };

    private static readonly byte[] SnapshotTwoBytes =
    {
        0x14,
        0x1D, 0x00, 0x00, 0x00,
        0x02, 0x00,
        0x01, 0x00, 0x00, 0x01,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
        0x00, 0x00,
        0x00,
        0x64,
        0x1D, 0x00, 0x00, 0x00,
        0x02, 0x00, 0x00, 0x01,
        0x9C, 0xFF, 0xFF, 0xFF,
        0x32, 0x00, 0x00, 0x00,
        0x19, 0x00, 0x00, 0x00,
        0x00, 0x40,
        0x02,
        0x50,
        0x1C, 0x00, 0x00, 0x00,
    };

    [Fact]
    public void Protocol_Hash_IsSchemaXorContent_AndContentIsZero()
    {
        Assert.Equal(0x4112C9FAu, Protocol.SchemaHash);
        Assert.Equal(0u, Protocol.ContentHash);
        Assert.Equal(Protocol.SchemaHash ^ Protocol.ContentHash, Protocol.Hash);
        Assert.Equal(Protocol.SchemaHash, Protocol.Hash);
    }

    [Fact]
    public void EntityClass_Player_IsOne_AndFirstIdMatchesSpec()
    {
        Assert.Equal(1, EntityClass.Player);
        Assert.Equal(2, EntityClass.Vehicle);
        Assert.Equal(16777217u, FirstPlayer.Value);
        Assert.Equal(33554433u, EntityId.FromClassAndCounter(EntityClass.Vehicle, 1).Value);
    }

    [Fact]
    public void Hello_GoldenRoundTrip()
    {
        var hello = new Hello(Protocol.Hash);
        Assert.Equal(HelloBytes, WireCodec.Encode(hello));
        Assert.True(WireCodec.TryDecode(HelloBytes, out Hello decoded));
        Assert.Equal(hello, decoded);
    }

    [Fact]
    public void HelloOk_GoldenRoundTrip()
    {
        var ok = new HelloOk(FirstPlayer, 0);
        Assert.Equal(HelloOkBytes, WireCodec.Encode(ok));
        Assert.True(WireCodec.TryDecode(HelloOkBytes, out HelloOk decoded));
        Assert.Equal(ok, decoded);
    }

    [Fact]
    public void HelloReject_GoldenRoundTrip()
    {
        var reject = new HelloReject(HelloRejectReason.ProtocolMismatch);
        Assert.Equal(HelloRejectBytes, WireCodec.Encode(reject));
        Assert.True(WireCodec.TryDecode(HelloRejectBytes, out HelloReject decoded));
        Assert.Equal(reject, decoded);
    }

    [Fact]
    public void HelloReject_VersionMismatch_GoldenRoundTrip()
    {
        var reject = new HelloReject(HelloRejectReason.VersionMismatch);
        Assert.Equal(HelloRejectVersionBytes, WireCodec.Encode(reject));
        Assert.True(WireCodec.TryDecode(HelloRejectVersionBytes, out HelloReject decoded));
        Assert.Equal(reject, decoded);
    }

    [Fact]
    public void WorldOffer_GoldenRoundTrip()
    {
        var offer = new WorldOffer(0x7F3A9C21, 0x821670054873680EUL);
        Assert.Equal(WorldOfferBytes, WireCodec.Encode(offer));
        Assert.True(WireCodec.TryDecode(WorldOfferBytes, out WorldOffer decoded));
        Assert.Equal(offer, decoded);
    }

    [Fact]
    public void RunSettings_Arcade_GoldenRoundTrip()
    {
        var settings = RunSettings.Arcade();
        Assert.Equal(RunSettingsArcadeBytes, WireCodec.Encode(settings));
        Assert.True(WireCodec.TryDecode(RunSettingsArcadeBytes, out RunSettings decoded));
        Assert.Equal(settings, decoded);
        Assert.Equal(0x33, RunSettingsArcadeBytes[0]);
    }

    [Fact]
    public void RunSettings_CustomStampsInvite_GoldenRoundTrip()
    {
        var settings = new RunSettings(
            2134567890,
            "small_island",
            new[] { "double_raids" },
            4,
            LobbyVisibility.Invite,
            "land",
            Protocol.SchemaHash,
            Protocol.ContentHash);
        Assert.Equal(RunSettingsCustomBytes, WireCodec.Encode(settings));
        Assert.True(WireCodec.TryDecode(RunSettingsCustomBytes, out RunSettings decoded));
        Assert.Equal(settings, decoded);
        Assert.Equal(new[] { "double_raids" }, decoded.Stamps);
    }

    [Fact]
    public void JoinState_EmptyPrepShift3_GoldenRoundTrip()
    {
        var join = new JoinState(
            0x7F3A9C21,
            0x821670054873680EUL,
            WorldDeltas.Empty,
            new RunState(RunPhase.Prep, 3, 0),
            Array.Empty<ContainerStamp>());
        Assert.Equal(JoinStateEmptyPrepBytes, WireCodec.Encode(join));
        Assert.True(WireCodec.TryDecode(JoinStateEmptyPrepBytes, out JoinState decoded));
        Assert.Equal(join, decoded);
        Assert.Equal(0x34, JoinStateEmptyPrepBytes[0]);
        Assert.Equal(52, (byte)MessageKind.JoinState);
    }

    [Fact]
    public void AccountHello_GoldenRoundTrip()
    {
        var hello = new AccountHello(7);
        var bytes = new byte[] { 0x35, 0x07, 0x00, 0x00, 0x00 };
        Assert.Equal(bytes, WireCodec.Encode(hello));
        Assert.True(WireCodec.TryDecode(bytes, out AccountHello decoded));
        Assert.Equal(hello, decoded);
        Assert.Equal(53, (byte)MessageKind.AccountHello);
    }

    [Fact]
    public void InputPacket_OneCmd_GoldenRoundTrip()
    {
        var cmd = new InputCmd(7, -127, 127, 32768, InputButtons.Sprint | InputButtons.Interact);
        var packet = new InputPacket(new[] { cmd });
        Assert.Equal(InputOneBytes, WireCodec.Encode(packet));
        Assert.True(WireCodec.TryDecode(InputOneBytes, out InputPacket? decoded));
        Assert.NotNull(decoded);
        Assert.Equal(new[] { cmd }, decoded!.Commands);
    }

    [Fact]
    public void InputPacket_ThreeCmds_NewestFirst_GoldenRoundTrip()
    {
        var newest = new InputCmd(10, 1, 0, 100, InputButtons.Sprint);
        var mid = new InputCmd(9, 0, -1, 200, InputButtons.Jump);
        var oldest = new InputCmd(8, -1, 1, 300, InputButtons.None);
        var packet = new InputPacket(new[] { newest, mid, oldest });
        Assert.Equal(InputThreeBytes, WireCodec.Encode(packet));
        Assert.True(WireCodec.TryDecode(InputThreeBytes, out InputPacket? decoded));
        Assert.NotNull(decoded);
        Assert.Equal(new[] { newest, mid, oldest }, decoded!.Commands);
    }

    [Fact]
    public void SnapshotPacket_OnePlayer_GoldenRoundTrip()
    {
        var player = new PlayerSnapshot(FirstPlayer, 0, 0, 0, 0, 0, 100, 29);
        var packet = new SnapshotPacket(29, new[] { player });
        Assert.Equal(SnapshotOneBytes, WireCodec.Encode(packet));
        Assert.True(WireCodec.TryDecode(SnapshotOneBytes, out SnapshotPacket? decoded));
        Assert.NotNull(decoded);
        Assert.Equal(29u, decoded!.ServerTick);
        Assert.Equal(new[] { player }, decoded.Players);
    }

    [Fact]
    public void SnapshotPacket_TwoPlayers_SignedPosition_GoldenRoundTrip()
    {
        var first = new PlayerSnapshot(FirstPlayer, 0, 0, 0, 0, 0, 100, 29);
        var second = new PlayerSnapshot(
            EntityId.FromClassAndCounter(EntityClass.Player, 2),
            -100, 50, 25, 16384, 2, 80, 28);
        var packet = new SnapshotPacket(29, new[] { first, second });
        Assert.Equal(SnapshotTwoBytes, WireCodec.Encode(packet));
        Assert.True(WireCodec.TryDecode(SnapshotTwoBytes, out SnapshotPacket? decoded));
        Assert.NotNull(decoded);
        Assert.Equal(29u, decoded!.ServerTick);
        Assert.Equal(new[] { first, second }, decoded.Players);
    }

    [Fact]
    public void TruncatedPayloads_FailDecode()
    {
        Assert.False(WireCodec.TryDecode(HelloBytes.AsSpan(0, HelloBytes.Length - 1), out Hello _));
        Assert.False(WireCodec.TryDecode(HelloOkBytes.AsSpan(0, HelloOkBytes.Length - 1), out HelloOk _));
        Assert.False(WireCodec.TryDecode(HelloRejectBytes.AsSpan(0, 1), out HelloReject _));
        Assert.False(WireCodec.TryDecode(WorldOfferBytes.AsSpan(0, WorldOfferBytes.Length - 1), out WorldOffer _));
        Assert.False(WireCodec.TryDecode(RunSettingsArcadeBytes.AsSpan(0, RunSettingsArcadeBytes.Length - 1), out RunSettings _));
        Assert.False(WireCodec.TryDecode(RunSettingsCustomBytes.AsSpan(0, RunSettingsCustomBytes.Length - 1), out RunSettings _));
        Assert.False(WireCodec.TryDecode(JoinStateEmptyPrepBytes.AsSpan(0, JoinStateEmptyPrepBytes.Length - 1), out JoinState _));
        Assert.False(WireCodec.TryDecode(InputOneBytes.AsSpan(0, InputOneBytes.Length - 1), out InputPacket? _));
        Assert.False(WireCodec.TryDecode(SnapshotOneBytes.AsSpan(0, SnapshotOneBytes.Length - 1), out SnapshotPacket? _));
        Assert.False(WireCodec.TryDecode(ReadOnlySpan<byte>.Empty, out Hello _));
    }

    [Fact]
    public void ExtraTrailingBytes_FailDecode()
    {
        Assert.False(WireCodec.TryDecode(AppendByte(HelloBytes), out Hello _));
        Assert.False(WireCodec.TryDecode(AppendByte(InputOneBytes), out InputPacket? _));
        Assert.False(WireCodec.TryDecode(AppendByte(SnapshotOneBytes), out SnapshotPacket? _));
        Assert.False(WireCodec.TryDecode(AppendByte(RunSettingsArcadeBytes), out RunSettings _));
        Assert.False(WireCodec.TryDecode(AppendByte(RunSettingsCustomBytes), out RunSettings _));
        Assert.False(WireCodec.TryDecode(AppendByte(JoinStateEmptyPrepBytes), out JoinState _));
    }

    [Fact]
    public void JoinState_InvalidPhaseOrShift_FailsDecode()
    {
        var badPhase = (byte[])JoinStateEmptyPrepBytes.Clone();
        badPhase[13] = 0xFF;
        Assert.False(WireCodec.TryDecode(badPhase, out JoinState _));

        var badShift = (byte[])JoinStateEmptyPrepBytes.Clone();
        badShift[14] = 0x00;
        Assert.False(WireCodec.TryDecode(badShift, out JoinState _));
    }

    [Fact]
    public void InputPacket_CountOutsideOneToThree_FailsDecode()
    {
        Assert.False(WireCodec.TryDecode(new byte[] { 0x0A, 0x00 }, out InputPacket? zero));
        Assert.Null(zero);

        var four = new byte[2 + 40];
        four[0] = 0x0A;
        four[1] = 0x04;
        Assert.False(WireCodec.TryDecode(four, out InputPacket? tooMany));
        Assert.Null(tooMany);
    }

    [Fact]
    public void InputPacket_Constructor_RejectsCountOutsideOneToThree()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new InputPacket(Array.Empty<InputCmd>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InputPacket(new InputCmd[4]));
    }

    [Fact]
    public void InputPacket_MutatingSourceAfterEncode_DoesNotChangeDecode()
    {
        var cmds = new[]
        {
            new InputCmd(3, 1, 0, 1, InputButtons.Sprint),
            new InputCmd(2, 0, 1, 2, InputButtons.None),
        };
        var encoded = WireCodec.Encode(new InputPacket(cmds));
        cmds[0] = new InputCmd(99, 0, 0, 0, InputButtons.Attack);

        Assert.True(WireCodec.TryDecode(encoded, out InputPacket? decoded));
        Assert.NotNull(decoded);
        Assert.Equal(3u, decoded!.Commands[0].Tick);
        Assert.Equal(InputButtons.Sprint, decoded.Commands[0].Buttons);
        Assert.Equal(2u, decoded.Commands[1].Tick);
    }

    [Fact]
    public void WrongKind_FailsDecode()
    {
        Assert.False(WireCodec.TryDecode(HelloBytes, out HelloOk _));
        Assert.False(WireCodec.TryDecode(HelloOkBytes, out Hello _));
        Assert.False(WireCodec.TryDecode(InputOneBytes, out SnapshotPacket? _));
        Assert.False(WireCodec.TryDecode(SnapshotOneBytes, out InputPacket? _));
    }

    private static byte[] AppendByte(byte[] source)
    {
        var copy = new byte[source.Length + 1];
        Array.Copy(source, copy, source.Length);
        return copy;
    }
}
