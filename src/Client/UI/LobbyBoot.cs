using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public static class LobbyBoot
{
    public const string HostName = "Jules";

    public static LobbySnapshot Arcade() =>
        Snapshot(ready: false);

    public static LobbySnapshot ArcadeReady() =>
        Snapshot(ready: true);

    private static LobbySnapshot Snapshot(bool ready)
    {
        var settings = RunSettings.Arcade();
        return new LobbySnapshot(
            in settings,
            new[] { new LobbyPlayer(HostName, settings.HostKit, ready, Host: true) });
    }
}
