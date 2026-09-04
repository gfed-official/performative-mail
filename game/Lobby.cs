using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class Lobby : Control
{
    public const string SeedPath = "SeedLabel";
    public const string ArchetypePath = "ArchetypeLabel";
    public const string KitPath = "KitDropdown";
    public const string ReadyPath = "ReadyLabel";
    public const string StartPath = "StartLabel";
    public const string PlayerListPath = "PlayerList";

    private Label _seed = null!;
    private Label _archetype = null!;
    private OptionButton _kit = null!;
    private Label _ready = null!;
    private Label _start = null!;
    private Label _players = null!;
    private bool _startEnabled;

    public override void _Ready() => CacheNodes();

    public void Bind(in LobbyFrame frame)
    {
        CacheNodes();
        _seed.Text = frame.SeedLabel;
        _archetype.Text = frame.ArchetypeLabel;
        _kit.Clear();
        _kit.AddItem(frame.KitLabel);
        _kit.Select(0);
        _ready.Text = frame.ReadyLabel;
        _start.Text = frame.StartLabel;
        _startEnabled = frame.StartEnabled;
        _players.Text = frame.PlayerList;
    }

    public string Dump(string caseName)
    {
        CacheNodes();
        return
            $"LOBBY_DUMP case={caseName}\n" +
            $"visible={(Visible ? "true" : "false")}\n" +
            $"SeedLabel={_seed.Text}\n" +
            $"ArchetypeLabel={_archetype.Text}\n" +
            $"KitLabel={KitText()}\n" +
            $"ReadyLabel={_ready.Text}\n" +
            $"StartLabel={_start.Text}\n" +
            $"StartEnabled={(_startEnabled ? "true" : "false")}\n" +
            $"PlayerList={_players.Text.Replace('\n', '|')}";
    }

    private string KitText()
    {
        if (_kit.ItemCount == 0 || _kit.Selected < 0)
            return "";
        return _kit.GetItemText(_kit.Selected);
    }

    private void CacheNodes()
    {
        if (_seed is not null)
            return;
        _seed = GetNode<Label>("%" + SeedPath);
        _archetype = GetNode<Label>("%" + ArchetypePath);
        _kit = GetNode<OptionButton>("%" + KitPath);
        _ready = GetNode<Label>("%" + ReadyPath);
        _start = GetNode<Label>("%" + StartPath);
        _players = GetNode<Label>("%" + PlayerListPath);
    }
}
