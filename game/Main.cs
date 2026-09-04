using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Game.Net;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class Main : Node3D
{
    private PlaySessionMachine _session = null!;
    private PawnStage _pawns = null!;
    private WorldStage _world = null!;
    private Camera3D _camera = null!;
    private LineEdit _address = null!;
    private Label _status = null!;
    private Button _host = null!;
    private Button _join = null!;
    private Control _menuChrome = null!;
    private FirstPersonLookState _look;

    private bool _walk;
    private bool _reported;
    private bool _inspectHud;
    private bool _inspectOverlay;
    private bool _inspectLobby;
    private bool _inspectOverlays;
    private bool _overlayHeld;
    private bool _pauseHeld;
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
    private PauseMenu _pauseMenu = null!;
    private readonly PauseMenuState _pause = new();
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
        BuildPause();
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_pause.IsOpen || _menuChrome.Visible)
            return;
        if (@event is not InputEventMouseMotion motion)
            return;
        FirstPersonLook.ApplyMouse(ref _look, motion.Relative.X, motion.Relative.Y);
    }

    public override void _PhysicsProcess(double delta)
    {
        var intent = _pause.IsOpen
            ? new MoveIntent(0, 0, _look.Yaw, InputButtons.None)
            : _walk
                ? new MoveIntent(0, sbyte.MaxValue, _look.Yaw, InputButtons.None)
                : InputSampler.Sample(in _look);
        var state = _session.Pump(WallNow(), in intent);
        Render(state);
        if (!_pause.IsOpen)
            PollOverlayToggle(state);
        PollPause(state);
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
                ShowMenuChrome(true);
                SetMouseCaptured(false);
                HidePlayUi();
                _pawns.DespawnAll();
                _status.Text = "Host a game, or join a friend by LAN IP.";
                break;
            case PlaySession.Connecting:
                ShowMenuChrome(false);
                SetMouseCaptured(false);
                HidePlayUi();
                _pawns.DespawnAll();
                break;
            case PlaySession.Playing playing:
                ShowMenuChrome(false);
                SetMouseCaptured(!_pause.IsOpen);
                _pawns.Sync(playing.Pawns);
                _world.Sync(playing.World);
                BindHud(playing.Hud);
                if (_overlay.IsOpen && playing.Overlay is OverlayReplica overlay)
                    BindOverlay(overlay);
                ApplyFirstPersonCamera(playing);
                break;
            case PlaySession.Failed failed:
                ShowMenuChrome(true);
                SetMouseCaptured(false);
                HidePlayUi();
                _pawns.DespawnAll();
                _status.Text = failed.Reason.Message();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private void ShowMenuChrome(bool visible)
    {
        _menuChrome.Visible = visible;
        _host.Disabled = !visible;
        _join.Disabled = !visible;
        _lobby.Visible = false;
    }

    private void HidePlayUi()
    {
        _hud.Visible = false;
        _world.Clear();
        _overlay.Close();
    }

    private void ApplyFirstPersonCamera(PlaySession.Playing playing)
    {
        if (!_pawns.TryLocalEye(playing.Pawns, out var eye))
            return;
        _camera.Position = new Vector3(eye.X, eye.Y, eye.Z);
        _camera.Rotation = new Vector3(_look.PitchRadians, eye.YawRadians, 0f);
    }

    private void SetMouseCaptured(bool captured)
    {
        Input.MouseMode = captured
            ? Input.MouseModeEnum.Captured
            : Input.MouseModeEnum.Visible;
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

        _camera = new Camera3D
        {
            Position = new Vector3(0f, FirstPersonLook.EyeHeightMeters, 0f),
            Current = true,
        };
        AddChild(_camera);

        _world = new WorldStage();
        AddChild(_world);

        _pawns = new PawnStage();
        AddChild(_pawns);
    }

    private void BuildMenu()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        _menuChrome = new MarginContainer();
        _menuChrome.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _menuChrome.AddThemeConstantOverride("margin_left", 16);
        _menuChrome.AddThemeConstantOverride("margin_top", 16);
        _menuChrome.AddThemeConstantOverride("margin_right", 16);
        layer.AddChild(_menuChrome);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        _menuChrome.AddChild(column);

        _status = new Label { Text = "Host a game, or join a friend by LAN IP." };
        column.AddChild(_status);

        var form = new HBoxContainer();
        form.AddThemeConstantOverride("separation", 8);
        column.AddChild(form);

        _host = new Button { Text = "Host" };
        _host.Pressed += () => _session.Host();
        form.AddChild(_host);

        _address = new LineEdit
        {
            PlaceholderText = "127.0.0.1",
            CustomMinimumSize = new Vector2(220, 0),
            Text = "127.0.0.1",
        };
        form.AddChild(_address);

        _join = new Button { Text = "Join" };
        _join.Pressed += OnJoinPressed;
        form.AddChild(_join);
    }

    private void BuildHud()
    {
        var packed = GD.Load<PackedScene>("res://scenes/hud.tscn");
        _hud = packed.Instantiate<Hud>();
        var layer = new CanvasLayer { Layer = 10 };
        AddChild(layer);
        layer.AddChild(_hud);
        _hud.Visible = false;
    }

    private void BindHud(in HudSnapshot snapshot)
    {
        _hud.Visible = true;
        _hud.Bind(HudFrame.From(in snapshot));
    }

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

    private void BuildPause()
    {
        _pauseMenu = new PauseMenu();
        var layer = new CanvasLayer { Layer = 15 };
        AddChild(layer);
        layer.AddChild(_pauseMenu);
        _pauseMenu.ChoicePicked = OnPauseChoice;
        _pauseMenu.Bind(_pause.Frame, _pause.IsOpen);
    }

    private void PollOverlayToggle(PlaySession state)
    {
        bool held = Input.IsPhysicalKeyPressed(Key.Tab) || Input.IsPhysicalKeyPressed(Key.Y);
        if (held && !_overlayHeld)
        {
            if (!_overlay.IsOpen &&
                state is PlaySession.Playing playing &&
                playing.Overlay is OverlayReplica live)
            {
                BindOverlay(live);
            }

            _overlay.Toggle();
        }

        _overlayHeld = held;
    }

    private void PollPause(PlaySession state)
    {
        if (state is not PlaySession.Playing)
        {
            if (_pause.IsOpen)
                ClosePause();
            _pauseHeld = InputSampler.MenuHeld();
            return;
        }

        if (_pause.IsOpen && _pause.Snapshot.ClockPaused != _session.ClockPaused)
        {
            _pause.SetClockPaused(_session.ClockPaused);
            BindPause(state);
        }

        bool held = InputSampler.MenuHeld();
        bool edge = held && !_pauseHeld;
        _pauseHeld = held;
        if (!edge)
            return;

        if (!_pause.IsOpen)
        {
            OpenPause(state);
            return;
        }

        if (!_pause.Back())
            ClosePause();
        else
            BindPause(state);
    }

    private void OpenPause(PlaySession state)
    {
        _overlay.Close();
        _pause.Open(_session.TrySetClockPaused(true));
        BindPause(state);
    }

    private void BindPause(PlaySession state)
    {
        var frame = _pause.Frame;
        if (state is PlaySession.Playing { Role: SessionRole.Listening listening })
        {
            string baseStatus = frame.StatusLabel;
            string join = "Join " + listening.Advertisement;
            frame = frame with
            {
                StatusLabel = baseStatus.Length == 0 ? join : baseStatus + " · " + join,
            };
        }

        _pauseMenu.Bind(frame, true);
    }

    private void ClosePause()
    {
        _session.TrySetClockPaused(false);
        _pause.Close();
        _pauseMenu.Bind(_pause.Frame, false);
    }

    private void OnPauseChoice(string id)
    {
        _pause.Apply(id);
        if (_pause.WantsLeave)
        {
            ClosePause();
            _session.Leave();
            return;
        }

        if (!_pause.IsOpen)
        {
            ClosePause();
            return;
        }

        BindPause(_session.State);
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
            ShowMenuChrome(true);
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
            var hud = HudFrame.From(playing.Hud);
            json.Append(",\"local\":");
            json.Append(playing.LocalPlayer.Value);
            json.Append(",\"worldHash\":\"0x");
            json.Append((playing.World is { } tables
                ? WorldHash.Compute(tables)
                : 0UL).ToString("X16"));
            json.Append('\"');
            json.Append(",\"phase\":\"");
            json.Append(playing.Hud.Phase);
            json.Append('\"');
            json.Append(",\"shift\":");
            json.Append(playing.Hud.Shift);
            json.Append(",\"wallet\":");
            json.Append(playing.Hud.Wallet.Value);
            json.Append(",\"quota\":");
            json.Append(playing.Hud.Quota.Value);
            json.Append(",\"hudShift\":\"");
            json.Append(hud.ShiftLabel);
            json.Append('\"');
            json.Append(",\"hudPhase\":\"");
            json.Append(hud.PhaseLabel);
            json.Append('\"');
            json.Append(",\"hudTimer\":\"");
            json.Append(hud.TimerLabel);
            json.Append('\"');
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
