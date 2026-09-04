using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class Results : Control
{
    public const string ScorePath = "ScoreLabel";
    public const string SeedPath = "SeedLabel";

    private Label _score = null!;
    private Label _seed = null!;

    public override void _Ready() => CacheNodes();

    public void Bind(in ResultsFrame frame)
    {
        CacheNodes();
        _score.Text = frame.ScoreLabel;
        _seed.Text = frame.SeedLabel;
    }

    public string Dump(string caseName)
    {
        CacheNodes();
        return
            $"RESULTS_DUMP case={caseName}\n" +
            $"visible={(Visible ? "true" : "false")}\n" +
            $"ScoreLabel={_score.Text}\n" +
            $"SeedLabel={_seed.Text}";
    }

    private void CacheNodes()
    {
        if (_score is not null)
            return;
        _score = GetNode<Label>("%" + ScorePath);
        _seed = GetNode<Label>("%" + SeedPath);
    }
}
