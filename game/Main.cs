using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Game.Net;
using PerformativeMail.Sim.Core;

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
    private string? _reportPath;
    private int _quitAfterMs;
    private ulong _startedUsec;

    public override void _Ready()
    {
        _session = new PlaySessionMachine(new GodotEnetStack());
        _startedUsec = Time.GetTicksUsec();
        BuildWorld();
        BuildMenu();
        ApplyArgs(OS.GetCmdlineUserArgs());
    }

    public override void _ExitTree() => _session.Dispose();

    public override void _PhysicsProcess(double delta)
    {
        var intent = _walk
            ? new MoveIntent(0, sbyte.MaxValue, 0, InputButtons.None)
            : InputSampler.Sample();
        var state = _session.Pump(WallNow(), in intent);
        Render(state);
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
        if (_quitAfterMs <= 0)
            return;

        ulong elapsedMs = (Time.GetTicksUsec() - _startedUsec) / 1000;
        if (elapsedMs < (ulong)_quitAfterMs)
            return;

        if (_reportPath is not null)
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
