using System.Globalization;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Client.UI;

public readonly record struct ResultsFrame(string ScoreLabel, string SeedLabel)
{
    public static ResultsFrame From(in ResultsPayload payload) =>
        new(
            payload.Score.ToString(CultureInfo.InvariantCulture),
            payload.SeedString);
}
