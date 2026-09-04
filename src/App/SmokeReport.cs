using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.World;

namespace PerformativeMail.App;

public readonly record struct SmokeReportUi(bool OverlayOpen, bool DebugOpen);

public static class SmokeReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Render(PlaySession session, in SmokeReportUi ui)
    {
        switch (session)
        {
            case PlaySession.Playing playing:
                return JsonSerializer.Serialize(ToPlaying(playing, in ui), JsonOptions);
            case PlaySession.Failed failed:
                return JsonSerializer.Serialize(
                    new FailedDocument(failed.GetType().Name, failed.Reason.Message()),
                    JsonOptions);
            case PlaySession.Menu:
                return JsonSerializer.Serialize(new StateDocument(session.GetType().Name), JsonOptions);
            case PlaySession.Connecting:
                return JsonSerializer.Serialize(new StateDocument(session.GetType().Name), JsonOptions);
            default:
                throw new ArgumentOutOfRangeException(nameof(session), session, null);
        }
    }

    public static void Write(string path, PlaySession session, in SmokeReportUi ui) =>
        File.WriteAllText(path, Render(session, in ui));

    private static PlayingDocument ToPlaying(PlaySession.Playing playing, in SmokeReportUi ui)
    {
        var hud = HudFrame.From(playing.Hud);
        var pawns = new PawnDocument[playing.Pawns.Count];
        for (int i = 0; i < pawns.Length; i++)
        {
            var pawn = playing.Pawns[i];
            pawns[i] = new PawnDocument(
                pawn.Id.Value,
                pawn.Role.ToString(),
                pawn.Pose.Xcm,
                pawn.Pose.Ycm);
        }

        return new PlayingDocument(
            playing.GetType().Name,
            playing.LocalPlayer.Value,
            FormatWorldHash(playing.World),
            playing.Hud.Phase.ToString(),
            playing.Hud.Shift,
            playing.Hud.Wallet.Value,
            playing.Hud.Quota.Value,
            hud.ShiftLabel,
            hud.PhaseLabel,
            hud.TimerLabel,
            pawns,
            CountEntities(playing.World),
            ui.OverlayOpen,
            ui.DebugOpen);
    }

    private static string FormatWorldHash(WorldTables? world) =>
        "0x" + (world is { } tables ? WorldHash.Compute(tables) : 0UL).ToString("X16");

    private static WorldEntityCountsDocument CountEntities(WorldTables? world)
    {
        if (world is null)
            return new WorldEntityCountsDocument(0, 0, 0, 0);
        return new WorldEntityCountsDocument(1, 1, world.Houses.Length, world.Houses.Length);
    }

    private sealed record StateDocument(
        [property: JsonPropertyName("state")] string State);

    private sealed record FailedDocument(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("error")] string Error);

    private sealed record PawnDocument(
        [property: JsonPropertyName("id")] uint Id,
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("x")] int X,
        [property: JsonPropertyName("y")] int Y);

    private sealed record WorldEntityCountsDocument(
        [property: JsonPropertyName("postOffices")] int PostOffices,
        [property: JsonPropertyName("intakes")] int Intakes,
        [property: JsonPropertyName("houses")] int Houses,
        [property: JsonPropertyName("mailboxes")] int Mailboxes);

    private sealed record PlayingDocument(
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("local")] uint Local,
        [property: JsonPropertyName("worldHash")] string WorldHash,
        [property: JsonPropertyName("phase")] string Phase,
        [property: JsonPropertyName("shift")] byte Shift,
        [property: JsonPropertyName("wallet")] int Wallet,
        [property: JsonPropertyName("quota")] int Quota,
        [property: JsonPropertyName("hudShift")] string HudShift,
        [property: JsonPropertyName("hudPhase")] string HudPhase,
        [property: JsonPropertyName("hudTimer")] string HudTimer,
        [property: JsonPropertyName("pawns")] PawnDocument[] Pawns,
        [property: JsonPropertyName("worldEntityCounts")] WorldEntityCountsDocument WorldEntityCounts,
        [property: JsonPropertyName("overlayOpen")] bool OverlayOpen,
        [property: JsonPropertyName("debugOpen")] bool DebugOpen);
}
