using System;
using PerformativeMail.Sim.Net;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Sim.Tests.Run;

public sealed class RunSettingsTests
{
    [Fact]
    public void Arcade_PinsLobbyDefaultsAndProtocolHashes()
    {
        var settings = RunSettings.Arcade();
        Assert.Equal(0x7F3A9C21u, settings.Seed);
        Assert.Equal("small_island", settings.Archetype);
        Assert.Empty(settings.Stamps);
        Assert.Equal(8, settings.MaxPlayers);
        Assert.Equal(LobbyVisibility.Friends, settings.Visibility);
        Assert.Equal("land", settings.HostKit);
        Assert.Equal(Protocol.SchemaHash, settings.ProtocolHash);
        Assert.Equal(Protocol.ContentHash, settings.ContentHash);
        Assert.Equal(0x4112C9FAu, settings.ProtocolHash);
        Assert.Equal(0u, settings.ContentHash);
    }

    [Fact]
    public void EmptyStamps_AreAllowed()
    {
        var settings = Valid(stamps: Array.Empty<string>());
        Assert.Empty(settings.Stamps);
        Assert.Equal(RunSettings.Arcade(), settings);
    }

    [Fact]
    public void SameStampIds_AreEqual()
    {
        var a = Valid(stamps: new[] { "double_raids" });
        var b = Valid(stamps: new[] { "double_raids" });
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(new[] { "double_raids" }, a.Stamps);
    }

    [Fact]
    public void MaxPlayers_ZeroAndNine_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Valid(maxPlayers: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Valid(maxPlayers: 9));
    }

    [Fact]
    public void EmptyArchetype_Throws()
    {
        Assert.Throws<ArgumentException>(() => Valid(archetype: ""));
    }

    [Fact]
    public void PascalCaseArchetype_Throws()
    {
        Assert.Throws<ArgumentException>(() => Valid(archetype: "SmallIsland"));
    }

    [Fact]
    public void EmptyHostKit_Throws()
    {
        Assert.Throws<ArgumentException>(() => Valid(hostKit: ""));
    }

    [Fact]
    public void StampThatIsNotSnakeCase_Throws()
    {
        Assert.Throws<ArgumentException>(() => Valid(stamps: new[] { "DoubleRaids" }));
    }

    private static RunSettings Valid(
        uint seed = 0x7F3A9C21,
        string archetype = "small_island",
        string[]? stamps = null,
        byte maxPlayers = 8,
        LobbyVisibility visibility = LobbyVisibility.Friends,
        string hostKit = "land")
        => new(
            seed,
            archetype,
            stamps ?? Array.Empty<string>(),
            maxPlayers,
            visibility,
            hostKit,
            Protocol.SchemaHash,
            Protocol.ContentHash);
}
