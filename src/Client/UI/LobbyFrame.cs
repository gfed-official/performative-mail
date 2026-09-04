using System.Collections.Generic;
using System.Text;

namespace PerformativeMail.Client.UI;

public readonly record struct LobbyFrame(
    string SeedLabel,
    string ArchetypeLabel,
    string KitLabel,
    string ReadyLabel,
    string StartLabel,
    bool StartEnabled,
    string PlayerList)
{
    public const string ReadyText = "ready";
    public const string NotReadyText = "not ready";
    public const string StartText = "Start";
    public const string HostBadge = "host";
    public const string GuestBadge = "guest";

    public static LobbyFrame From(in LobbySnapshot snapshot)
    {
        var settings = snapshot.Settings;
        bool allReady = AllReady(snapshot.Players);
        return new LobbyFrame(
            FormatSeed(settings.Seed),
            settings.Archetype,
            settings.HostKit,
            allReady ? ReadyText : NotReadyText,
            StartText,
            allReady,
            FormatPlayers(snapshot.Players));
    }

    public static string FormatSeed(uint seed) => $"0x{seed:X8}";

    private static bool AllReady(IReadOnlyList<LobbyPlayer> players)
    {
        if (players.Count == 0)
            return false;
        for (int i = 0; i < players.Count; i++)
        {
            if (!players[i].Ready)
                return false;
        }

        return true;
    }

    private static string FormatPlayers(IReadOnlyList<LobbyPlayer> players)
    {
        if (players.Count == 0)
            return "";

        var text = new StringBuilder();
        for (int i = 0; i < players.Count; i++)
        {
            if (i > 0)
                text.Append('\n');
            var player = players[i];
            text.Append(player.Name);
            text.Append(' ');
            text.Append(player.Host ? HostBadge : GuestBadge);
            text.Append(' ');
            text.Append(player.Kit);
            text.Append(' ');
            text.Append(player.Ready ? ReadyText : NotReadyText);
        }

        return text.ToString();
    }
}
