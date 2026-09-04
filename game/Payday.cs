using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class Payday : Control
{
    public const string EarnedPath = "EarnedLabel";
    public const string QuotaPath = "QuotaLabel";

    private Label _earned = null!;
    private Label _quota = null!;

    public override void _Ready() => CacheNodes();

    public void Bind(in PaydayFrame frame)
    {
        CacheNodes();
        _earned.Text = frame.EarnedLabel;
        _quota.Text = frame.QuotaLabel;
    }

    public string Dump(string caseName)
    {
        CacheNodes();
        return
            $"PAYDAY_DUMP case={caseName}\n" +
            $"visible={(Visible ? "true" : "false")}\n" +
            $"EarnedLabel={_earned.Text}\n" +
            $"QuotaLabel={_quota.Text}";
    }

    private void CacheNodes()
    {
        if (_earned is not null)
            return;
        _earned = GetNode<Label>("%" + EarnedPath);
        _quota = GetNode<Label>("%" + QuotaPath);
    }
}
