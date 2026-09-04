using System;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct DebugFrame(
    string ConnectionLabel,
    string RoleLabel,
    string TickLabel,
    string PhaseLabel,
    string ShiftLabel,
    string SeedLabel,
    string WorldHashLabel,
    string PlayerLabel,
    string WalletLabel,
    string AuthorityLabel,
    bool CanCheat)
{
    public const int WalletGrantCents = 1000;
    public const string ToggleKey = "F3";
    public const string Missing = "-";
    public const string HostRole = "host";
    public const string GuestRole = "guest";
    public const string HostAuthority = "host-only";
    public const string InspectAuthority = "inspect only";

    public static DebugFrame From(in DebugSnapshot snapshot) =>
        new(
            ConnectionName(snapshot.Connection),
            RoleName(in snapshot),
            snapshot.Tick is uint tick ? tick.ToString() : Missing,
            snapshot.Phase is RunPhase phase ? PhaseName(phase) : Missing,
            snapshot.Shift is byte shift ? shift.ToString() : Missing,
            snapshot.Seed is uint seed ? LobbyFrame.FormatSeed(seed) : Missing,
            snapshot.WorldHash is ulong hash ? $"0x{hash:X16}" : Missing,
            snapshot.LocalPlayer is uint player ? player.ToString() : Missing,
            snapshot.Wallet is Cents wallet ? FormatWallet(wallet) : Missing,
            snapshot.CanCheat ? HostAuthority : InspectAuthority,
            snapshot.CanCheat);

    private static string ConnectionName(DebugConnection connection) => connection switch
    {
        DebugConnection.Menu => "MENU",
        DebugConnection.Connecting => "CONNECTING",
        DebugConnection.Playing => "PLAYING",
        DebugConnection.Failed => "FAILED",
        _ => throw new ArgumentOutOfRangeException(nameof(connection), connection, null),
    };

    private static string RoleName(in DebugSnapshot snapshot)
    {
        if (snapshot.Host)
            return HostRole;
        if (snapshot.Connection is DebugConnection.Connecting or DebugConnection.Playing)
            return GuestRole;
        return Missing;
    }

    private static string PhaseName(RunPhase phase) => phase switch
    {
        RunPhase.Lobby => "LOBBY",
        RunPhase.Generating => "GENERATING",
        RunPhase.Prep => "PREP",
        RunPhase.Delivery => "DELIVERY",
        RunPhase.Raid => "RAID",
        RunPhase.Payday => "PAYDAY",
        RunPhase.Draft => "DRAFT",
        RunPhase.Results => "RESULTS",
        RunPhase.RunOver => "RUN OVER",
        RunPhase.Victory => "VICTORY",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    private static string FormatWallet(Cents wallet)
    {
        int value = wallet.Value;
        int abs = value < 0 ? -value : value;
        string amount = $"${abs / 100}.{abs % 100:D2}";
        return value < 0 ? "-" + amount : amount;
    }
}
