using Godot;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Content;

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
    public const string TeleportIntakePath = "TeleportIntakeButton";
    public const string TeleportMailboxPath = "TeleportMailboxButton";
    public const string GiveMailPath = "GiveMailButton";
    public const string OpenInventoryPath = "OpenInventoryButton";

    public event Action? GiveWalletPressed;
    public event Action? AdvancePhasePressed;
    public event Action? ResetPawnPressed;
    public event Action? TeleportIntakePressed;
    public event Action? TeleportMailboxPressed;
    public event Action? GiveMailPressed;
    public event Action? OpenInventoryPressed;
    public event Action<DebugSpawnId>? SpawnPressed;

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
    private Button _teleportIntake = null!;
    private Button _teleportMailbox = null!;
    private Button _giveMail = null!;
    private Button _openInventory = null!;
    private VBoxContainer _spawnColumn = null!;
    private readonly List<(DebugSpawnRow Row, Button Button)> _spawns = new();
    private bool _open;

    public bool IsOpen => _open && Visible;

    public override void _Ready()
    {
        Name = RootPath;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        PlaceChrome();
        BuildPanel();
        Viewport viewport = GetViewport();
        if (viewport is not null)
            viewport.SizeChanged += PlaceChrome;
    }

    public void Open()
    {
        PlaceChrome();
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
        PlaceChrome();
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
        _teleportIntake.Disabled = !frame.CanCheat;
        _teleportMailbox.Disabled = !frame.CanCheat;
        _giveMail.Disabled = !frame.CanCheat;
        _openInventory.Disabled = !frame.CanCheat;
        for (int i = 0; i < _spawns.Count; i++)
            _spawns[i].Button.Disabled = !frame.CanCheat;
        Visible = _open;
    }

    public void SetSpawns(IReadOnlyList<DebugSpawnRow> rows)
    {
        BuildPanel();
        foreach (var child in _spawnColumn.GetChildren())
            child.QueueFree();
        _spawns.Clear();
        if (rows is null)
            return;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var button = AddCheat(
                _spawnColumn,
                "Spawn_" + row.Id.ContentId,
                "Spawn " + row.Label,
                () => SpawnPressed?.Invoke(row.Id));
            _spawns.Add((row, button));
        }
    }

    public string Dump(string caseName)
    {
        BuildPanel();
        PlaceChrome();
        var size = Size;
        var global = GetGlobalRect();
        return
            $"DEBUG_DUMP case={caseName}\n" +
            $"visible={(IsOpen ? "true" : "false")}\n" +
            $"panelRect={size.X:0}x{size.Y:0}\n" +
            $"panelGlobal={global.Position.X:0},{global.Position.Y:0}\n" +
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
            $"TeleportIntake={Enabled(_teleportIntake)}\n" +
            $"TeleportMailbox={Enabled(_teleportMailbox)}\n" +
            $"GiveMail={Enabled(_giveMail)}\n" +
            $"OpenInventory={Enabled(_openInventory)}\n" +
            $"SpawnCount={_spawns.Count}\n" +
            $"Spawn.axe={SpawnState("axe")}\n" +
            $"Spawn.letter={SpawnState("letter")}\n" +
            $"Spawn.bike={SpawnState("bike")}\n" +
            $"ToggleKey={DebugFrame.ToggleKey}";
    }

    private void PlaceChrome()
    {
        Vector2 vp = Vector2.Zero;
        Window? window = GetWindow();
        if (window is not null)
            vp = window.Size;
        if (vp.X < 640 || vp.Y < 360)
            vp = GetViewport()?.GetVisibleRect().Size ?? Vector2.Zero;
        if (vp.X < 640 || vp.Y < 360)
            vp = new Vector2(1280, 720);
        SetAnchorsPreset(LayoutPreset.TopLeft);
        Position = new Vector2(vp.X - 320, 48);
        Size = new Vector2(304, Mathf.Max(220, vp.Y - 64));
    }

    private void BuildPanel()
    {
        if (_connection is not null)
            return;

        var panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(288, 0),
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.16f, 0.96f),
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10,
        };
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(panel);

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
        _teleportIntake = AddCheat(column, TeleportIntakePath, "Teleport to Intake", () => TeleportIntakePressed?.Invoke());
        _teleportMailbox = AddCheat(column, TeleportMailboxPath, "Teleport to mailbox", () => TeleportMailboxPressed?.Invoke());
        _giveMail = AddCheat(column, GiveMailPath, "Give mail", () => GiveMailPressed?.Invoke());
        _openInventory = AddCheat(column, OpenInventoryPath, "Open inventory", () => OpenInventoryPressed?.Invoke());

        column.AddChild(new Label { Text = "SPAWN" });
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 220),
            MouseFilter = MouseFilterEnum.Stop,
        };
        column.AddChild(scroll);
        _spawnColumn = new VBoxContainer();
        _spawnColumn.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_spawnColumn);
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

    private string SpawnState(string contentId)
    {
        for (int i = 0; i < _spawns.Count; i++)
        {
            if (_spawns[i].Row.Id.ContentId != contentId)
                continue;
            return Enabled(_spawns[i].Button);
        }

        return "missing";
    }
}
