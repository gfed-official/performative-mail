using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Game.Net;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;

namespace PerformativeMail.Game;

public partial class Main : Node3D
{
    private PlaySessionMachine _session = null!;
    private PawnStage _pawns = null!;
    private Camera3D _camera = null!;
    private LineEdit _address = null!;
    private Label _status = null!;
    private Button _host = null!;
    private Button _join = null!;
    private Button _leave = null!;
    private Control _form = null!;

    private bool _walk;
    private bool _reported;
    private bool _inspectHud;
    private bool _inspectOverlay;
    private bool _inspectLobby;
    private bool _inspectOverlays;
    private bool _overlayHeld;
    private string? _reportPath;
    private string? _hudDumpPath;
    private string? _overlayDumpPath;
    private string? _lobbyDumpPath;
    private string? _overlaysDumpPath;
    private int _quitAfterMs;
    private ulong _startedUsec;
    private Hud _hud = null!;
    private Lobby _lobby = null!;
    private InventoryOverlay _overlay = null!;
    private Payday _payday = null!;
    private Draft _draft = null!;
    private Results _results = null!;
    private DebugMenu? _debug;
    private bool _debugHeld;
    private bool _inspectDebug;
    private string? _debugDumpPath;

    public override void _Ready()
    {
        _session = new PlaySessionMachine(new GodotEnetStack());
        _startedUsec = Time.GetTicksUsec();
        BuildWorld();
        BuildMenu();
        BuildHud();
        BuildLobby();
        BuildOverlay();
        BuildPhaseOverlays();
        ApplyArgs(OS.GetCmdlineUserArgs());
        if (_inspectHud)
        {
            InspectHud();
            return;
        }

        if (_inspectOverlay)
        {
            InspectOverlay();
            return;
        }

        if (_inspectLobby)
        {
            InspectLobby();
            return;
        }

        if (_inspectOverlays)
        {
            InspectOverlays();
            return;
        }

        if (OS.IsDebugBuild() || _inspectDebug)
            BuildDebugMenu();

        if (_inspectDebug)
        {
            InspectDebug();
            return;
        }

        BindOverlay(OverlayBootReplica.Build());
        GD.Print("performative-mail boot ok");
    }

    public override void _ExitTree() => _session.Dispose();

    public override void _PhysicsProcess(double delta)
    {
        var intent = _walk
            ? new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None)
            : InputSampler.Sample();
        var state = _session.Pump(WallNow(), in intent);
        Render(state);
        PollOverlayToggle();
        PollDebugToggle();
        if (_debug is { IsOpen: true })
            BindDebug(_session.Inspect());
        MaybeFinish(state);
    }

    private void Render(PlaySession state)
    {
        switch (state)
        {
            case PlaySession.Menu:
                ShowForm(enabled: true);
                _pawns.DespawnAll();
                _status.Text = "Host a game, or join a friend by LAN IP.";
                break;
            case PlaySession.Connecting connecting:
                ShowForm(enabled: false);
                _leave.Disabled = false;
                _pawns.DespawnAll();
                _status.Text = connecting.Describe();
                break;
            case PlaySession.Playing playing:
                ShowForm(enabled: false);
                _leave.Disabled = false;
                _pawns.Sync(playing.Pawns);
                _status.Text = StatusFor(playing);
                if (_pawns.TryLocalOrigin(playing.Pawns, out var focus))
                {
                    _camera.Position = focus + new Vector3(0f, 9f, 8f);
                    _camera.LookAt(focus);
                }
                break;
            case PlaySession.Failed failed:
                ShowForm(enabled: true);
                _pawns.DespawnAll();
                _status.Text = failed.Reason.Message();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private void ShowForm(bool enabled)
    {
        _form.Visible = true;
        _host.Disabled = !enabled;
        _join.Disabled = !enabled;
        _leave.Disabled = enabled;
    }

    private static string StatusFor(PlaySession.Playing playing)
    {
        if (playing.Role is SessionRole.Listening listening)
            return $"Hosting. Friends join {listening.Advertisement}. WASD to walk.";
        return $"Joined. {playing.Pawns.Count} pawn(s) in view. WASD to walk.";
    }

    private void BuildWorld()
    {
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50f, -30f, 0f),
            ShadowEnabled = false,
        };
        AddChild(light);

        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.45f, 0.72f, 0.92f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.5f,
            },
        };
        AddChild(env);

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(40f, 40f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.22f, 0.38f, 0.22f) },
        };
        AddChild(ground);

        _camera = new Camera3D
        {
            Position = new Vector3(0f, 9f, 8f),
            Current = true,
        };
        AddChild(_camera);
        _camera.LookAt(Vector3.Zero);

        _pawns = new PawnStage();
        AddChild(_pawns);
    }

    private void BuildMenu()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var root = new MarginContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        root.AddThemeConstantOverride("margin_left", 16);
        root.AddThemeConstantOverride("margin_top", 16);
        root.AddThemeConstantOverride("margin_right", 16);
        layer.AddChild(root);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        root.AddChild(column);

        _status = new Label { Text = "Host a game, or join a friend by LAN IP." };
        column.AddChild(_status);

        _form = new HBoxContainer();
        _form.AddThemeConstantOverride("separation", 8);
        column.AddChild(_form);

        _host = new Button { Text = "Host" };
        _host.Pressed += () => _session.Host();
        _form.AddChild(_host);

        _address = new LineEdit
        {
            PlaceholderText = "127.0.0.1",
            CustomMinimumSize = new Vector2(220, 0),
            Text = "127.0.0.1",
        };
        _form.AddChild(_address);

        _join = new Button { Text = "Join" };
        _join.Pressed += OnJoinPressed;
        _form.AddChild(_join);

        _leave = new Button { Text = "Leave", Disabled = true };
        _leave.Pressed += () => _session.Leave();
        column.AddChild(_leave);
    }

    private void BuildHud()
    {
        var packed = GD.Load<PackedScene>("res://scenes/hud.tscn");
        _hud = packed.Instantiate<Hud>();
        var layer = new CanvasLayer { Layer = 10 };
        AddChild(layer);
        layer.AddChild(_hud);
    }

    private void BindHud(in HudSnapshot snapshot) =>
        _hud.Bind(HudFrame.From(in snapshot));

    private void BuildLobby()
    {
        var packed = GD.Load<PackedScene>("res://scenes/lobby.tscn");
        _lobby = packed.Instantiate<Lobby>();
        var layer = new CanvasLayer { Layer = 9 };
        AddChild(layer);
        layer.AddChild(_lobby);
        _lobby.Visible = false;
    }

    private void BindLobby(in LobbySnapshot snapshot) =>
        _lobby.Bind(LobbyFrame.From(in snapshot));

    private void BuildPhaseOverlays()
    {
        var layer = new CanvasLayer { Layer = 12 };
        AddChild(layer);
        _payday = GD.Load<PackedScene>("res://scenes/payday.tscn").Instantiate<Payday>();
        layer.AddChild(_payday);
        _payday.Visible = false;
        _draft = GD.Load<PackedScene>("res://scenes/draft.tscn").Instantiate<Draft>();
        layer.AddChild(_draft);
        _draft.Visible = false;
        _results = GD.Load<PackedScene>("res://scenes/results.tscn").Instantiate<Results>();
        layer.AddChild(_results);
        _results.Visible = false;
    }

    private void BindPayday(in PaydaySnapshot snapshot) =>
        _payday.Bind(PaydayFrame.From(in snapshot));

    private void BindDraft(in DraftOffer offer) =>
        _draft.Bind(DraftFrame.From(in offer));

    private void BindResults(in ResultsPayload payload) =>
        _results.Bind(ResultsFrame.From(in payload));

    private void BuildOverlay()
    {
        _overlay = new InventoryOverlay();
        var layer = new CanvasLayer { Layer = 11 };
        AddChild(layer);
        layer.AddChild(_overlay);
        _overlay.Bind(OverlayFrame.From(OverlayBootReplica.Build()));
    }

    private void BindOverlay(in OverlayReplica replica) =>
        _overlay.Bind(OverlayFrame.From(in replica));

    private void PollOverlayToggle()
    {
        bool held = Input.IsPhysicalKeyPressed(Key.Tab) || Input.IsPhysicalKeyPressed(Key.Y);
        if (held && !_overlayHeld)
            _overlay.Toggle();
        _overlayHeld = held;
    }

    private void BuildDebugMenu()
    {
        _debug = new DebugMenu();
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        layer.AddChild(_debug);
        _debug.GiveWalletPressed += () => _session.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents));
        _debug.AdvancePhasePressed += () => _session.TryAdvancePhase();
        _debug.ResetPawnPressed += () => _session.TryResetLocalPawn();
    }

    private void BindDebug(in DebugSnapshot snapshot)
    {
        if (_debug is null)
            return;
        _debug.Bind(DebugFrame.From(in snapshot));
    }

    private void PollDebugToggle()
    {
        if (_debug is null)
            return;

        bool held = Input.IsPhysicalKeyPressed(Key.F3) || Input.IsPhysicalKeyPressed(Key.Quoteleft);
        if (held && !_debugHeld)
            _debug.Toggle();
        _debugHeld = held;
    }

    private void InspectDebug()
    {
        if (_debug is null)
            return;

        var dump = new StringBuilder();
        _debug.Open();
        BindDebug(DebugBoot.Placeholder());
        dump.AppendLine(_debug.Dump("open"));
        _debug.Close();
        dump.AppendLine(_debug.Dump("closed"));
        dump.AppendLine("DEBUG_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_debugDumpPath is not null)
            File.WriteAllText(_debugDumpPath, text);
        GetTree().Quit();
    }

    private void InspectHud()
    {
        var dump = new StringBuilder();
        BindHud(HudBoot.Placeholder());
        dump.AppendLine(_hud.Dump("match"));
        BindHud(InspectMismatch());
        dump.AppendLine(_hud.Dump("mismatch"));
        dump.AppendLine("HUD_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_hudDumpPath is not null)
            File.WriteAllText(_hudDumpPath, text);
        GetTree().Quit();
    }

    private void InspectOverlay()
    {
        BindHud(HudBoot.Placeholder());
        BindOverlay(OverlayBootReplica.Build());
        var dump = new StringBuilder();
        _overlay.Open();
        dump.Append(_overlay.Dump("open"));
        dump.Append(_hud.Dump("open"));
        dump.Append('\n');
        _overlay.Close();
        dump.Append(_overlay.Dump("closed"));
        dump.Append(_hud.Dump("closed"));
        dump.Append('\n');
        dump.AppendLine("OVERLAY_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_overlayDumpPath is not null)
            File.WriteAllText(_overlayDumpPath, text);
        GetTree().Quit();
    }

    private void InspectLobby()
    {
        _lobby.Visible = true;
        var dump = new StringBuilder();
        BindLobby(LobbyBoot.Arcade());
        dump.AppendLine(_lobby.Dump("arcade"));
        BindLobby(LobbyBoot.ArcadeReady());
        dump.AppendLine(_lobby.Dump("ready"));
        dump.AppendLine("LOBBY_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_lobbyDumpPath is not null)
            File.WriteAllText(_lobbyDumpPath, text);
        GetTree().Quit();
    }

    private void InspectOverlays()
    {
        _payday.Visible = true;
        _draft.Visible = true;
        _results.Visible = true;
        BindPayday(PhaseOverlayBoot.Payday());
        BindDraft(PhaseOverlayBoot.Draft());
        BindResults(PhaseOverlayBoot.Results());
        var dump = new StringBuilder();
        dump.AppendLine(_payday.Dump("payday"));
        dump.AppendLine(_draft.Dump("draft"));
        dump.AppendLine(_results.Dump("results"));
        dump.AppendLine("PHASE_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_overlaysDumpPath is not null)
            File.WriteAllText(_overlaysDumpPath, text);
        GetTree().Quit();
    }

    private static HudSnapshot InspectMismatch() =>
        DeliveryStub(new InteractPrompt.Deliver("13 Larch Lane", "8 Oak Street"));

    private static HudSnapshot DeliveryStub(InteractPrompt interact) =>
        new(RunPhase.Delivery, 1, 0, 2700, new Cents(1820), interact,
            new Cents(640), new Cents(2214), 23);

    private void OnJoinPressed()
    {
        if (!JoinTarget.TryParse(_address.Text, SessionOptions.DefaultPort, out var target))
        {
            _status.Text = "Enter a host like 192.168.1.20 or 192.168.1.20:7777.";
            return;
        }

        _session.Join(target);
    }

    private void ApplyArgs(string[] args)
    {
        string? join = null;
        bool host = false;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--host")
                host = true;
            else if (arg == "--walk")
                _walk = true;
            else if (arg.StartsWith("--join=", StringComparison.Ordinal))
                join = arg.Substring("--join=".Length);
            else if (arg.StartsWith("--report=", StringComparison.Ordinal))
                _reportPath = arg.Substring("--report=".Length);
            else if (arg == "--inspect-hud")
                _inspectHud = true;
            else if (arg == "--inspect-overlay")
                _inspectOverlay = true;
            else if (arg == "--inspect-lobby")
                _inspectLobby = true;
            else if (arg == "--inspect-overlays")
                _inspectOverlays = true;
            else if (arg == "--inspect-debug")
                _inspectDebug = true;
            else if (arg.StartsWith("--hud-dump=", StringComparison.Ordinal))
                _hudDumpPath = arg.Substring("--hud-dump=".Length);
            else if (arg.StartsWith("--overlay-dump=", StringComparison.Ordinal))
                _overlayDumpPath = arg.Substring("--overlay-dump=".Length);
            else if (arg.StartsWith("--lobby-dump=", StringComparison.Ordinal))
                _lobbyDumpPath = arg.Substring("--lobby-dump=".Length);
            else if (arg.StartsWith("--overlays-dump=", StringComparison.Ordinal))
                _overlaysDumpPath = arg.Substring("--overlays-dump=".Length);
            else if (arg.StartsWith("--debug-dump=", StringComparison.Ordinal))
                _debugDumpPath = arg.Substring("--debug-dump=".Length);
            else if (arg.StartsWith("--quit-after-ms=", StringComparison.Ordinal) &&
                     int.TryParse(arg.AsSpan("--quit-after-ms=".Length), out var ms))
                _quitAfterMs = ms;
        }

        if (host)
            _session.Host();
        else if (join is not null && JoinTarget.TryParse(join, SessionOptions.DefaultPort, out var target))
            _session.Join(target);
    }

    private void MaybeFinish(PlaySession state)
    {
        if (_reportPath is not null && !_reported &&
            state is PlaySession.Playing playing && playing.Pawns.Count >= 2)
        {
            WriteReport(state, _reportPath);
            _reported = true;
        }

        if (_quitAfterMs <= 0)
            return;

        ulong elapsedMs = (Time.GetTicksUsec() - _startedUsec) / 1000;
        if (elapsedMs < (ulong)_quitAfterMs)
            return;

        if (_reportPath is not null && !_reported)
            WriteReport(state, _reportPath);
        GetTree().Quit();
    }

    private static void WriteReport(PlaySession state, string path)
    {
        var json = new StringBuilder();
        json.Append("{\"state\":\"");
        json.Append(state.GetType().Name);
        json.Append('\"');
        if (state is PlaySession.Playing playing)
        {
            json.Append(",\"local\":");
            json.Append(playing.LocalPlayer.Value);
            json.Append(",\"pawns\":[");
            for (int i = 0; i < playing.Pawns.Count; i++)
            {
                if (i > 0)
                    json.Append(',');
                var pawn = playing.Pawns[i];
                json.Append("{\"id\":");
                json.Append(pawn.Id.Value);
                json.Append(",\"role\":\"");
                json.Append(pawn.Role);
                json.Append("\",\"x\":");
                json.Append(pawn.Pose.Xcm);
                json.Append(",\"y\":");
                json.Append(pawn.Pose.Ycm);
                json.Append('}');
            }

            json.Append(']');
        }
        else if (state is PlaySession.Failed failed)
        {
            json.Append(",\"error\":\"");
            json.Append(failed.Reason.Message().Replace("\"", "'"));
            json.Append('\"');
        }

        json.Append('}');
        File.WriteAllText(path, json.ToString());
    }

    private static TimeSpan WallNow() =>
        TimeSpan.FromTicks((long)Time.GetTicksUsec() * 10);
}
