using System.Globalization;

namespace PerformativeMail.Client.UI;

public readonly record struct PaydayFrame(string EarnedLabel, string QuotaLabel)
{
    public static PaydayFrame From(in PaydaySnapshot snapshot) =>
        new(
            snapshot.Earned.Value.ToString(CultureInfo.InvariantCulture),
            snapshot.Quota.Value.ToString(CultureInfo.InvariantCulture));
}
