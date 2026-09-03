using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Net;

namespace PerformativeMail.Client;

public enum PawnRole : byte
{
    Local = 1,
    Remote = 2,
}

public readonly record struct PawnView(
    EntityId Id,
    PlayerPose Pose,
    PawnRole Role,
    byte Palette,
    string DisplayName);

public static class PawnPalette
{
    public const int Count = 8;

    public static byte IndexFor(EntityId id) => (byte)(id.Counter % Count);

    public static string NameFor(EntityId id) => $"Player {id.Counter}";

    public static (byte R, byte G, byte B) Rgb(byte paletteIndex)
    {
        switch (paletteIndex % Count)
        {
            case 0: return (56, 132, 255);
            case 1: return (255, 99, 71);
            case 2: return (46, 204, 113);
            case 3: return (241, 196, 15);
            case 4: return (155, 89, 182);
            case 5: return (26, 188, 156);
            case 6: return (230, 126, 34);
            case 7: return (236, 240, 241);
            default:
                throw new ArgumentOutOfRangeException(nameof(paletteIndex), paletteIndex, null);
        }
    }
}

public sealed class PawnViewTable
{
    private readonly List<PawnView> _visible = new();

    public IReadOnlyList<PawnView> Visible => _visible;

    public void Clear() => _visible.Clear();

    public void Refresh(ClientRuntime client, TimeSpan serverTime)
    {
        if (client is null)
            throw new ArgumentNullException(nameof(client));

        _visible.Clear();
        if (client.LocalPlayer is EntityId local &&
            client.TryPresent(local, serverTime, out var localPose))
        {
            _visible.Add(new PawnView(local, localPose, PawnRole.Local, PawnPalette.IndexFor(local), "You"));
        }

        var snapshot = client.LastSnapshot;
        if (snapshot is null)
            return;

        for (int i = 0; i < snapshot.Players.Count; i++)
        {
            var id = snapshot.Players[i].Id;
            if (client.LocalPlayer is EntityId owner && id == owner)
                continue;
            if (!client.TryPresent(id, serverTime, out var pose))
                continue;

            _visible.Add(new PawnView(
                id,
                pose,
                PawnRole.Remote,
                PawnPalette.IndexFor(id),
                PawnPalette.NameFor(id)));
        }
    }
}
