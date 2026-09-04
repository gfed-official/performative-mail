using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class Draft : Control
{
    public const string Card1Path = "Card1Label";
    public const string Card2Path = "Card2Label";
    public const string Card3Path = "Card3Label";

    private Label _card1 = null!;
    private Label _card2 = null!;
    private Label _card3 = null!;

    public override void _Ready() => CacheNodes();

    public void Bind(in DraftFrame frame)
    {
        CacheNodes();
        _card1.Text = frame.Card1Label;
        _card2.Text = frame.Card2Label;
        _card3.Text = frame.Card3Label;
    }

    public string Dump(string caseName)
    {
        CacheNodes();
        return
            $"DRAFT_DUMP case={caseName}\n" +
            $"visible={(Visible ? "true" : "false")}\n" +
            $"Card1Label={_card1.Text}\n" +
            $"Card2Label={_card2.Text}\n" +
            $"Card3Label={_card3.Text}";
    }

    private void CacheNodes()
    {
        if (_card1 is not null)
            return;
        _card1 = GetNode<Label>("%" + Card1Path);
        _card2 = GetNode<Label>("%" + Card2Path);
        _card3 = GetNode<Label>("%" + Card3Path);
    }
}
