using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class DebugMenu : Control
{
    public const string RootPath = "DebugRoot";
    public const string ConnectionPath = "ConnectionLabel";
    public const string RolePath = "RoleLabel";
    public const string TickPath = "TickLabel";
    public const string PhasePath = "PhaseLabel";
    public const string ShiftPath = "ShiftLabel";
    public const string SeedPath = "SeedLabel";
    public const string WorldHashPath = "WorldHashLabel";
    public const string PlayerPath = "PlayerLabel";
    public const string WalletPath = "WalletLabel";
    public const string AuthorityPath = "AuthorityLabel";
    public const string GiveWalletPath = "GiveWalletButton";
    public const string AdvancePhasePath = "AdvancePhaseButton";
    public const string ResetPawnPath = "ResetPawnButton";

    public event Action? GiveWalletPressed;
    public event Action? AdvancePhasePressed;
    public event Action? ResetPawnPressed;

    private Label _connection = null!;
    private Label _role = null!;
    private Label _tick = null!;
    private Label _phase = null!;
    private Label _shift = null!;
    private Label _seed = null!;
    private Label _worldHash = null!;
    private Label _player = null!;
    private Label _wallet = null!;
    private Label _authority = null!;
    private Button _giveWallet = null!;
    private Button _advancePhase = null!;
    private Button _resetPawn = null!;
    private bool _open;

    public bool IsOpen => _open && Visible;

    public override void _Ready()
    {
        Name = RootPath;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        BuildPanel();
    }

    public void Open()
    {
        Visible = true;
        _open = true;
    }

    public void Close()
    {
        Visible = false;
        _open = false;
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Bind(in DebugFrame frame)
    {
        BuildPanel();
        _connection.Text = frame.ConnectionLabel;
        _role.Text = frame.RoleLabel;
        _tick.Text = frame.TickLabel;
        _phase.Text = frame.PhaseLabel;
        _shift.Text = frame.ShiftLabel;
        _seed.Text = frame.SeedLabel;
        _worldHash.Text = frame.WorldHashLabel;
        _player.Text = frame.PlayerLabel;
        _wallet.Text = frame.WalletLabel;
        _authority.Text = frame.AuthorityLabel;
        _giveWallet.Disabled = !frame.CanCheat;
        _advancePhase.Disabled = !frame.CanCheat;
        _resetPawn.Disabled = !frame.CanCheat;
        Visible = _open;
    }

    public string Dump(string caseName)
    {
        BuildPanel();
        return
            $"DEBUG_DUMP case={caseName}\n" +
            $"visible={(IsOpen ? "true" : "false")}\n" +
            $"ConnectionLabel={_connection.Text}\n" +
            $"RoleLabel={_role.Text}\n" +
            $"TickLabel={_tick.Text}\n" +
            $"PhaseLabel={_phase.Text}\n" +
            $"ShiftLabel={_shift.Text}\n" +
            $"SeedLabel={_seed.Text}\n" +
            $"WorldHashLabel={_worldHash.Text}\n" +
            $"PlayerLabel={_player.Text}\n" +
            $"WalletLabel={_wallet.Text}\n" +
            $"AuthorityLabel={_authority.Text}\n" +
            $"GiveWallet={Enabled(_giveWallet)}\n" +
            $"AdvancePhase={Enabled(_advancePhase)}\n" +
            $"ResetPawn={Enabled(_resetPawn)}\n" +
            $"ToggleKey={DebugFrame.ToggleKey}";
    }

    private void BuildPanel()
    {
        if (_connection is not null)
            return;

        var margin = new MarginContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        margin.SetAnchorsPreset(LayoutPreset.TopRight);
        margin.GrowHorizontal = GrowDirection.Begin;
        margin.OffsetLeft = -320;
        margin.OffsetTop = 48;
        margin.OffsetRight = -16;
        margin.AddThemeConstantOverride("margin_left", 0);
        margin.AddThemeConstantOverride("margin_top", 0);
        margin.AddThemeConstantOverride("margin_right", 0);
        AddChild(margin);

        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(288, 0),
        };
        margin.AddChild(panel);

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left", 12);
        inner.AddThemeConstantOverride("margin_top", 10);
        inner.AddThemeConstantOverride("margin_right", 12);
        inner.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(inner);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        inner.AddChild(column);

        column.AddChild(new Label { Text = "DEBUG" });
        _connection = AddRow(column, "conn", ConnectionPath);
        _role = AddRow(column, "role", RolePath);
        _tick = AddRow(column, "tick", TickPath);
        _phase = AddRow(column, "phase", PhasePath);
        _shift = AddRow(column, "shift", ShiftPath);
        _seed = AddRow(column, "seed", SeedPath);
        _worldHash = AddRow(column, "hash", WorldHashPath);
        _player = AddRow(column, "player", PlayerPath);
        _wallet = AddRow(column, "wallet", WalletPath);
        _authority = new Label { Name = AuthorityPath, Text = DebugFrame.InspectAuthority };
        column.AddChild(_authority);

        _giveWallet = AddCheat(column, GiveWalletPath, "Give $10.00", () => GiveWalletPressed?.Invoke());
        _advancePhase = AddCheat(column, AdvancePhasePath, "Advance phase", () => AdvancePhasePressed?.Invoke());
        _resetPawn = AddCheat(column, ResetPawnPath, "Reset pawn", () => ResetPawnPressed?.Invoke());
    }

    private static Label AddRow(VBoxContainer column, string prefix, string name)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        column.AddChild(row);
        row.AddChild(new Label { Text = prefix, CustomMinimumSize = new Vector2(56, 0) });
        var value = new Label { Name = name, Text = DebugFrame.Missing };
        row.AddChild(value);
        return value;
    }

    private static Button AddCheat(VBoxContainer column, string name, string text, Action pressed)
    {
        var button = new Button { Name = name, Text = text, Disabled = true };
        button.Pressed += pressed;
        column.AddChild(button);
        return button;
    }

    private static string Enabled(Button button) => button.Disabled ? "disabled" : "enabled";
}
