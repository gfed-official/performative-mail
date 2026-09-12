using System.Text;
using Godot;
using PerformativeMail.App;
using PerformativeMail.Client;
using PerformativeMail.Client.UI;
using PerformativeMail.Game.Net;
using PerformativeMail.Sim.Building;
using PerformativeMail.Sim.Content;
using PerformativeMail.Sim.Core;
using PerformativeMail.Sim.Inventory;
using PerformativeMail.Sim.Mail;
using PerformativeMail.Sim.Movement;
using PerformativeMail.Sim.Run;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class Main : Node3D
{
    private PlaySessionMachine _session = null!;
    private PawnStage _pawns = null!;
    private WorldStage _world = null!;
    private ConstructStage _constructs = null!;
    private Camera3D _menuCamera = null!;
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
    private bool _inspectMap;
    private bool _inspectLobby;
    private bool _inspectOverlays;
    private bool _inspectShop;
    private bool _overlayHeld;
    private bool _mapHeld;
    private bool _pauseHeld;
    private bool _shopHeld;
    private string? _reportPath;
    private string? _hudDumpPath;
    private string? _overlayDumpPath;
    private string? _mapDumpPath;
    private string? _lobbyDumpPath;
    private string? _overlaysDumpPath;
    private string? _shopDumpPath;
    private int _quitAfterMs;
    private ulong _startedUsec;
    private Hud _hud = null!;
    private Lobby _lobby = null!;
    private InventoryOverlay _overlay = null!;
    private MapOverlay _map = null!;
    private Payday _payday = null!;
    private Draft _draft = null!;
    private Results _results = null!;
    private Shop _shop = null!;
    private RunPhase _shopPhaseSeen;
    private PauseMenu _pauseMenu = null!;
    private readonly PauseMenuState _pause = new();
    private DebugMenu? _debug;
    private bool _debugHeld;
    private bool _inspectDebug;
    private bool _inspectBuild;
    private string? _debugDumpPath;
    private string? _buildDumpPath;
    private string? _worldDumpPath;
    private string? _debugHelper;
    private bool _holdInteract;
    private HudSnapshot _boundHud;
    private bool _hudBound;
    private CompassFrame _boundCompass;
    private bool _compassBound;
    private OverlayStamp _boundOverlay;
    private bool _overlayBound;
    private ShopFrame _boundShop;
    private bool _shopBound;
    private bool _playUiHidden = true;
    private bool _usingMenuCamera = true;
    private bool? _mouseCaptured;
    private int _hotbarSlot = InputSampler.DefaultHotbarSlot;
    private BuildBar _buildBar = null!;
    private BuildGhost _buildGhost = null!;
    private bool _buildHeld;
    private bool _rotateHeld;
    private bool _pipetteHeld;
    private bool _placeHeld;

    public override void _Ready()
    {
        _session = new PlaySessionMachine(new GodotEnetStack());
        _startedUsec = Time.GetTicksUsec();
        BuildWorld();
        BuildMenu();
        BuildHud();
        BuildLobby();
        BuildOverlay();
        BuildMap();
        BuildPhaseOverlays();
        BuildPause();
        BuildBuildMode();
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

        if (_inspectMap)
        {
            InspectMap();
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

        if (_inspectShop)
        {
            InspectShop();
            return;
        }

        if (OS.IsDebugBuild() || _inspectDebug)
            BuildDebugMenu();

        if (_inspectDebug)
        {
            InspectDebug();
            return;
        }

        if (_inspectBuild)
        {
            InspectBuild();
            return;
        }

        BindOverlay(OverlayBootReplica.Build());
        GD.Print("performative-mail boot ok");
    }

    public override void _ExitTree() => _session.Dispose();

    public override void _Input(InputEvent @event)
    {
        if (_pause.IsOpen || _menuChrome.Visible || _shop.IsOpen || _map.IsOpen)
            return;
        if (_session.Build is { IsOpen: true })
        {
            if (InputSampler.TryHotbarSlot(@event, out int category))
            {
                _session.Build.SelectCategoryIndex(category);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (InputSampler.TryHotbarWheel(@event, out int cycle))
            {
                _session.Build.Cycle(cycle);
                GetViewport().SetInputAsHandled();
            }

            return;
        }

        if (InputSampler.TryHotbarSlot(@event, out int slot))
        {
            SelectHotbar(slot);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!InputSampler.TryHotbarWheel(@event, out int delta))
            return;
        SelectHotbar(_hotbarSlot + delta);
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_pause.IsOpen || _menuChrome.Visible || _overlay.IsOpen || _map.IsOpen || _shop.IsOpen)
            return;
        if (@event is not InputEventMouseMotion motion)
            return;
        FirstPersonLook.ApplyMouse(ref _look, motion.Relative.X, motion.Relative.Y);
    }

    public override void _PhysicsProcess(double delta)
    {
        var intent = _pause.IsOpen || _shop.IsOpen
            ? new MoveIntent(0, 0, _look.Yaw, InputButtons.None)
            : _walk
                ? new MoveIntent(0, sbyte.MaxValue, _look.Yaw, InputButtons.None)
                : InputSampler.Sample(in _look);
        if (_holdInteract && !_pause.IsOpen && !_shop.IsOpen)
            intent = new MoveIntent(intent.AxisX, intent.AxisY, intent.Yaw, intent.Buttons | InputButtons.Interact);
        var state = _session.Pump(WallNow(), in intent);
        Render(state);
        if (!_pause.IsOpen)
        {
            PollOverlayToggle(state);
            PollMapToggle(state);
            PollShopToggle(state);
        }
        PollPause(state);
        PollDebugToggle();
        PollBuild(state);
        if (_debug is { IsOpen: true })
            BindDebug(_session.Inspect());
        MaybeApplyDebugHelper(state);
        if (state is PlaySession.Playing)
            SetMouseCaptured(!_pause.IsOpen && !_overlay.IsOpen && !_map.IsOpen && !_shop.IsOpen);
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
                UseMenuCamera();
                SetStatus("Host a game, or join a friend by LAN IP.");
                break;
            case PlaySession.Connecting:
                ShowMenuChrome(false);
                SetMouseCaptured(false);
                HidePlayUi();
                UseMenuCamera();
                break;
            case PlaySession.Playing playing:
                ShowMenuChrome(false);
                SetMouseCaptured(!_pause.IsOpen && !_overlay.IsOpen && !_map.IsOpen && !_shop.IsOpen);
                _usingMenuCamera = false;
                _pawns.Sync(playing.Pawns, _look.PitchRadians, HeldMailKind(playing), HeldMailDistrict(playing));
                _pawns.SyncVehicles(playing.Vehicles);
                _world.Sync(playing.World);
                _world.SyncHarvest(playing.Resources);
                _constructs.Sync(playing.Constructs);
                BindHud(playing.Hud);
                BindCompass(playing);
                if (playing.Overlay is OverlayReplica overlay)
                    BindOverlay(overlay);
                BindMap(playing);
                SyncShop(playing);
                break;
            case PlaySession.Failed failed:
                ShowMenuChrome(true);
                SetMouseCaptured(false);
                HidePlayUi();
                UseMenuCamera();
                SetStatus(failed.Reason.Message());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private void ShowMenuChrome(bool visible)
    {
        if (_menuChrome.Visible != visible)
            _menuChrome.Visible = visible;
        if (_host.Disabled == visible)
            _host.Disabled = !visible;
        if (_join.Disabled == visible)
            _join.Disabled = !visible;
        if (_lobby.Visible)
            _lobby.Visible = false;
    }

    private void HidePlayUi()
    {
        if (_playUiHidden)
            return;
        _playUiHidden = true;
        _hudBound = false;
        _compassBound = false;
        _overlayBound = false;
        _shopBound = false;
        _hud.Visible = false;
        _world.Clear();
        _constructs.Clear();
        _overlay.Close();
        _map.Close();
        _shop.Close();
        _shopPhaseSeen = default;
        _session.CloseBuild();
        _buildBar.Visible = false;
        _buildGhost.Visible = false;
    }

    private void UseMenuCamera()
    {
        if (_usingMenuCamera)
            return;
        _usingMenuCamera = true;
        _pawns.DespawnAll();
        _menuCamera.Current = true;
    }

    private void SetMouseCaptured(bool captured)
    {
        if (_mouseCaptured == captured)
            return;
        _mouseCaptured = captured;
        Input.MouseMode = captured
            ? Input.MouseModeEnum.Captured
            : Input.MouseModeEnum.Visible;
    }

    private void BuildWorld()
    {
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-50f, -30f, 0f),
            LightEnergy = 0.7f,
            ShadowEnabled = true,
            ShadowBlur = 2f,
        };
        AddChild(light);

        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky
                {
                    SkyMaterial = new ProceduralSkyMaterial
                    {
                        SkyTopColor = new Color(0.49f, 0.72f, 0.91f), // #7EB8E8
                        SkyHorizonColor = new Color(0.773f, 0.863f, 0.941f), // #C5DCF0
                        GroundHorizonColor = new Color(0.773f, 0.863f, 0.941f),
                        GroundBottomColor = new Color(0.44f, 0.66f, 0.42f), // #6FA86A
                    },
                },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.659f, 0.784f, 0.878f), // #A8C8E0
                AmbientLightEnergy = 0.4f,
                FogEnabled = true,
                FogMode = Godot.Environment.FogModeEnum.Depth,
                FogLightColor = new Color(0.659f, 0.784f, 0.878f), // #A8C8E0
                FogDepthBegin = 40f,
                FogDepthEnd = 120f,
            },
        };
        AddChild(env);
        AddGrassGround();

        _menuCamera = new Camera3D
        {
            Name = "MenuCamera",
            Position = new Vector3(0f, FirstPersonLook.EyeHeightMeters, 8f),
            Current = true,
        };
        AddChild(_menuCamera);
        _menuCamera.LookAt(new Vector3(0f, FirstPersonLook.EyeHeightMeters, 0f));

        _world = new WorldStage();
        AddChild(_world);

        _constructs = new ConstructStage();
        AddChild(_constructs);

        _pawns = new PawnStage();
        AddChild(_pawns);
    }

    private void AddGrassGround()
    {
        var slab = WorldTilePlacement.SmallIslandGround();
        AddChild(new MeshInstance3D
        {
            Name = "Grass",
            Mesh = new BoxMesh { Size = new Vector3(slab.SizeX, slab.SizeY, slab.SizeZ) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.44f, 0.66f, 0.42f), // #6FA86A
                Roughness = 0.85f,
            },
            Position = new Vector3(slab.X, slab.Y, slab.Z),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
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

    private void SetStatus(string text)
    {
        if (_status.Text != text)
            _status.Text = text;
    }

    private void BindHud(in HudSnapshot snapshot)
    {
        _playUiHidden = false;
        if (!_hud.Visible)
            _hud.Visible = true;
        if (_hudBound && HudFrame.SameDisplay(in _boundHud, in snapshot))
            return;
        _boundHud = snapshot;
        _hudBound = true;
        _hud.Bind(HudFrame.From(in snapshot));
    }

    private void BindCompass(PlaySession.Playing playing)
    {
        var pose = PlayerPose.Origin;
        for (int i = 0; i < playing.Pawns.Count; i++)
        {
            if (playing.Pawns[i].Id != playing.LocalPlayer)
                continue;
            pose = playing.Pawns[i].Pose;
            break;
        }

        BindCompass(CompassFrame.From(playing.World, in pose));
    }

    private void BindCompass(in CompassFrame frame)
    {
        if (_compassBound && CompassFrame.SameDisplay(in _boundCompass, in frame))
            return;
        _boundCompass = frame;
        _compassBound = true;
        _hud.BindCompass(in frame);
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
        _shop = GD.Load<PackedScene>("res://scenes/shop.tscn").Instantiate<Shop>();
        layer.AddChild(_shop);
        _shop.Visible = false;
        _shop.BuyPressed = OnShopBuy;
    }

    private void BindPayday(in PaydaySnapshot snapshot) =>
        _payday.Bind(PaydayFrame.From(in snapshot));

    private void BindDraft(in DraftOffer offer) =>
        _draft.Bind(DraftFrame.From(in offer));

    private void BindResults(in ResultsPayload payload) =>
        _results.Bind(ResultsFrame.From(in payload));

    private void BindShop(in ShopFrame frame)
    {
        if (_shopBound && ShopFrame.SameDisplay(in _boundShop, in frame))
            return;
        _boundShop = frame;
        _shopBound = true;
        _shop.Bind(frame);
    }

    private void BuildOverlay()
    {
        _overlay = new InventoryOverlay();
        var layer = new CanvasLayer { Layer = 11 };
        AddChild(layer);
        layer.AddChild(_overlay);
        _overlay.CellPicked = OnOverlayCellPicked;
        _overlay.Bind(OverlayFrame.From(OverlayBootReplica.Build()));
        _overlay.SelectCell("hotbar", (byte)_hotbarSlot, 0);
    }

    private void BuildMap()
    {
        _map = new MapOverlay();
        var layer = new CanvasLayer { Layer = 13 };
        AddChild(layer);
        layer.AddChild(_map);
        _map.LivePingRequested = OnLivePingRequested;
        _map.Bind(null, null, 0);
    }

    private void BindMap(PlaySession.Playing playing)
    {
        if (!_map.IsOpen)
            return;
        _map.Bind(playing.World, playing.Overlay, playing.Hud.Now, playing.Pings);
    }

    private void OnLivePingRequested(TileCoord tile)
    {
        if (_session.State is PlaySession.Playing)
            _session.TryPlacePing(tile, _map.PingKind);
        else
            _map.TryPlacePing(tile);
    }

    private void BindOverlay(in OverlayReplica replica)
    {
        var stamp = replica.Stamp();
        if (_overlayBound && stamp == _boundOverlay)
            return;
        _boundOverlay = stamp;
        _overlayBound = true;
        var frame = OverlayFrame.From(in replica);
        _overlay.Bind(frame);
        _hud.BindHotbar(frame.Hotbar, _hotbarSlot);
        _overlay.SelectCell("hotbar", (byte)_hotbarSlot, 0);
        _hud.SelectHotbar(_hotbarSlot);
    }

    private void BuildPause()
    {
        _pauseMenu = new PauseMenu();
        var layer = new CanvasLayer { Layer = 15 };
        AddChild(layer);
        layer.AddChild(_pauseMenu);
        _pauseMenu.ChoicePicked = OnPauseChoice;
        _pauseMenu.Bind(_pause.Frame, _pause.IsOpen);
    }

    private void BuildBuildMode()
    {
        _buildBar = new BuildBar();
        var layer = new CanvasLayer { Layer = 13 };
        AddChild(layer);
        layer.AddChild(_buildBar);
        _buildBar.CategoryPicked = category => _session.Build?.SetCategory(category);
        _buildBar.ChoicePicked = id => _session.Build?.Select(id);
        _buildGhost = new BuildGhost();
        AddChild(_buildGhost);
    }

    private void PollBuild(PlaySession state)
    {
        if (state is not PlaySession.Playing playing)
        {
            _session.CloseBuild();
            _buildBar.Visible = false;
            _buildGhost.Visible = false;
            _buildHeld = InputSampler.BuildHeld();
            _rotateHeld = InputSampler.RotateHeld();
            _pipetteHeld = InputSampler.PipetteHeld();
            _placeHeld = InputSampler.PlaceHeld();
            return;
        }

        bool build = InputSampler.BuildHeld();
        if (build && !_buildHeld && !_pause.IsOpen)
        {
            _overlay.Close();
            _map.Close();
            _shop.Close();
            _session.TryToggleBuild();
        }

        _buildHeld = build;
        if (_session.Build is not { IsOpen: true } mode)
        {
            _buildBar.Visible = false;
            _buildGhost.Visible = false;
            _rotateHeld = InputSampler.RotateHeld();
            _pipetteHeld = InputSampler.PipetteHeld();
            _placeHeld = InputSampler.PlaceHeld();
            return;
        }

        bool rotate = InputSampler.RotateHeld();
        if (rotate && !_rotateHeld)
            mode.Rotate();
        _rotateHeld = rotate;

        bool aim = TryAimTile(playing, out var tile);
        bool pipette = InputSampler.PipetteHeld();
        if (pipette && !_pipetteHeld && aim)
            _session.TryPipetteAt(tile);
        _pipetteHeld = pipette;

        bool place = InputSampler.PlaceHeld();
        if (place && !_placeHeld && aim && !_pause.IsOpen && !_overlay.IsOpen && !_map.IsOpen && !_shop.IsOpen)
            _session.TryPlaceAt(tile);
        _placeHeld = place;

        bool valid = true;
        string reason = "";
        if (aim && _session.TryGhost(tile, out var hint))
        {
            valid = hint.Valid;
            reason = hint.Reason;
            var origin = WorldTilePlacement.TileCenter(tile, (playing.World?.TileCm ?? 200) / 100f);
            _buildGhost.Bind(true, new Vector3(origin.X, origin.Y, origin.Z), (playing.World?.TileCm ?? 200) / 100f, valid);
        }
        else
            _buildGhost.Visible = false;

        _buildBar.Bind(mode.Frame(valid, reason));
    }

    private bool TryAimTile(PlaySession.Playing playing, out TileCoord tile)
    {
        tile = default;
        for (int i = 0; i < playing.Pawns.Count; i++)
        {
            if (playing.Pawns[i].Role != PawnRole.Local)
                continue;
            return _session.TryAimTile(playing.Pawns[i].Pose, _look.PitchRadians, out tile);
        }

        return false;
    }

    private void InspectBuild()
    {
        var bundle = ContentBoot.Load(out _, out _);
        var mode = new BuildModeState(bundle.Buildings);
        mode.Open();
        var dump = new StringBuilder();
        _buildBar.Bind(mode.Frame(true, ""));
        dump.AppendLine(_buildBar.Dump("open"));
        _buildBar.Bind(mode.Frame(false, BuildRejectText.Of(PlaceReject.Street)));
        dump.AppendLine(_buildBar.Dump("street"));
        mode.Close();
        _buildBar.Bind(mode.Frame(true, ""));
        dump.AppendLine(_buildBar.Dump("closed"));
        dump.AppendLine("BUILD_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_buildDumpPath is not null)
            File.WriteAllText(_buildDumpPath, text);
        GetTree().Quit();
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

            if (!_overlay.IsOpen)
                _map.Close();
            _overlay.Toggle();
            if (_overlay.IsOpen)
            {
                _shop.Close();
                _session.CloseBuild();
            }
        }

        _overlayHeld = held;
    }

    private void PollMapToggle(PlaySession state)
    {
        bool held = InputSampler.MapHeld();
        if (held && !_mapHeld)
        {
            if (_map.IsOpen)
                _map.Close();
            else if (state is PlaySession.Playing playing)
            {
                _overlay.Close();
                _shop.Close();
                _session.CloseBuild();
                _map.Open();
                BindMap(playing);
            }
        }

        _mapHeld = held;
    }

    private void PollShopToggle(PlaySession state)
    {
        bool held = Input.IsPhysicalKeyPressed(Key.P);
        if (held && !_shopHeld)
        {
            if (_shop.IsOpen)
                _shop.Close();
            else if (state is PlaySession.Playing playing &&
                     ShopFrame.PhaseOpen(playing.Hud.Phase))
                TryOpenLiveShop(playing);
        }

        _shopHeld = held;
    }

    private void SyncShop(PlaySession.Playing playing)
    {
        if (!ShopFrame.PhaseOpen(playing.Hud.Phase))
        {
            _shop.Close();
            _shopPhaseSeen = playing.Hud.Phase;
            return;
        }

        if (playing.Hud.Phase == RunPhase.Payday && _shopPhaseSeen != RunPhase.Payday)
            TryOpenLiveShop(playing);
        _shopPhaseSeen = playing.Hud.Phase;
        if (_shop.IsOpen && _session.TryShop(out var frame))
            BindShop(frame);
    }

    private bool TryOpenLiveShop(PlaySession.Playing playing)
    {
        if (!ShopFrame.PhaseOpen(playing.Hud.Phase))
            return false;
        if (!_session.TryShop(out var frame))
            return false;
        _overlay.Close();
        _map.Close();
        _session.CloseBuild();
        BindShop(frame);
        _shop.Open();
        return true;
    }

    private void OnShopBuy(string shopItemId)
    {
        _session.TryBuy(shopItemId);
        if (_session.TryShop(out var frame))
            BindShop(frame);
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
        _map.Close();
        _shop.Close();
        _session.CloseBuild();
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
        _debug.TeleportIntakePressed += () => _session.TryTeleportToIntake();
        _debug.TeleportMailboxPressed += () => _session.TryTeleportToMailbox();
        _debug.GiveMailPressed += () => _session.TryGiveMail();
        _debug.OpenInventoryPressed += () =>
        {
            if (_session.State is PlaySession.Playing playing)
                TryOpenLiveOverlay(playing);
        };
        _debug.OpenShopPressed += () =>
        {
            if (_session.State is PlaySession.Playing playing)
                TryOpenLiveShop(playing);
        };
        _debug.SetSpawns(_session.SpawnCatalog.Rows);
        _debug.SpawnPressed += id => _session.TrySpawn(id);
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
        BindOverlay(OverlayBootReplica.Build());
        BindCompass(CompassBoot.Placeholder());
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
        BindCompass(CompassBoot.Placeholder());
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

    private void InspectMap()
    {
        var dump = new StringBuilder();
        _map.Bind(MapBoot.Tables(), MapBoot.Overlay(), 0);
        _map.Open();
        dump.Append(_map.Dump("open"));
        _map.ToggleChip("mail");
        dump.Append(_map.Dump("mail"));
        _map.ToggleChip("routes");
        _map.ToggleChip("resources");
        dump.Append(_map.Dump("filters"));
        _map.TryPlacePing(MapBoot.PingTile, 0);
        dump.Append(_map.Dump("ping"));
        _map.Close();
        dump.Append(_map.Dump("closed"));
        dump.AppendLine("MAP_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_mapDumpPath is not null)
            File.WriteAllText(_mapDumpPath, text);
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

    private void InspectShop()
    {
        BindShop(ShopBoot.Inspect());
        _shop.Open();
        var dump = new StringBuilder();
        dump.AppendLine(_shop.Dump("open"));
        _shop.Close();
        dump.AppendLine(_shop.Dump("closed"));
        dump.AppendLine("SHOP_DUMP_END");
        var text = dump.ToString();
        GD.Print(text);
        if (_shopDumpPath is not null)
            File.WriteAllText(_shopDumpPath, text);
        GetTree().Quit();
    }

    private static HudSnapshot InspectMismatch() =>
        DeliveryStub(new InteractPrompt.Deliver("13 Larch Lane", "8 Oak Street"));

    private static HudSnapshot DeliveryStub(InteractPrompt interact) =>
        new(RunPhase.Delivery, 1, 0, 2700, new Cents(1820), interact,
            new Cents(640), new Cents(2214), 23, 100, 0);

    private void OnJoinPressed()
    {
        if (!JoinTarget.TryParse(_address.Text, SessionOptions.DefaultPort, out var target))
        {
            ShowMenuChrome(true);
            SetStatus("Enter a host like 192.168.1.20 or 192.168.1.20:7777.");
            return;
        }

        _session.Join(target);
    }

    private void ApplyArgs(string[] args)
    {
        string? join = null;
        bool host = false;
        bool debugWorld = false;
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--host")
                host = true;
            else if (arg == "--debug-world")
                debugWorld = true;
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
            else if (arg == "--inspect-map")
                _inspectMap = true;
            else if (arg == "--inspect-lobby")
                _inspectLobby = true;
            else if (arg == "--inspect-overlays")
                _inspectOverlays = true;
            else if (arg == "--inspect-shop")
                _inspectShop = true;
            else if (arg == "--inspect-debug")
                _inspectDebug = true;
            else if (arg == "--inspect-build")
                _inspectBuild = true;
            else if (arg.StartsWith("--hud-dump=", StringComparison.Ordinal))
                _hudDumpPath = arg.Substring("--hud-dump=".Length);
            else if (arg.StartsWith("--overlay-dump=", StringComparison.Ordinal))
                _overlayDumpPath = arg.Substring("--overlay-dump=".Length);
            else if (arg.StartsWith("--map-dump=", StringComparison.Ordinal))
                _mapDumpPath = arg.Substring("--map-dump=".Length);
            else if (arg.StartsWith("--lobby-dump=", StringComparison.Ordinal))
                _lobbyDumpPath = arg.Substring("--lobby-dump=".Length);
            else if (arg.StartsWith("--overlays-dump=", StringComparison.Ordinal))
                _overlaysDumpPath = arg.Substring("--overlays-dump=".Length);
            else if (arg.StartsWith("--shop-dump=", StringComparison.Ordinal))
                _shopDumpPath = arg.Substring("--shop-dump=".Length);
            else if (arg.StartsWith("--debug-dump=", StringComparison.Ordinal))
                _debugDumpPath = arg.Substring("--debug-dump=".Length);
            else if (arg.StartsWith("--build-dump=", StringComparison.Ordinal))
                _buildDumpPath = arg.Substring("--build-dump=".Length);
            else if (arg.StartsWith("--world-dump=", StringComparison.Ordinal))
                _worldDumpPath = arg.Substring("--world-dump=".Length);
            else if (arg.StartsWith("--debug-helper=", StringComparison.Ordinal))
                _debugHelper = arg.Substring("--debug-helper=".Length);
            else if (arg.StartsWith("--quit-after-ms=", StringComparison.Ordinal) &&
                     int.TryParse(arg.AsSpan("--quit-after-ms=".Length), out var ms))
                _quitAfterMs = ms;
        }

        if (host && debugWorld)
            _session.HostDebug();
        else if (host)
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
        if (_worldDumpPath is not null && state is PlaySession.Playing)
            File.WriteAllText(_worldDumpPath, _world.Dump() + "\n" + _constructs.Dump());
        if (_overlayDumpPath is not null && state is PlaySession.Playing)
        {
            var dump = new StringBuilder();
            dump.Append(_overlay.Dump("live"));
            dump.AppendLine("OVERLAY_DUMP_END");
            File.WriteAllText(_overlayDumpPath, dump.ToString());
        }
        if (_hudDumpPath is not null && state is PlaySession.Playing)
        {
            var dump = new StringBuilder();
            dump.AppendLine(_hud.Dump("live"));
            dump.AppendLine("HUD_DUMP_END");
            File.WriteAllText(_hudDumpPath, dump.ToString());
        }
        if (_mapDumpPath is not null && state is PlaySession.Playing)
        {
            var dump = new StringBuilder();
            dump.Append(_map.Dump("live"));
            dump.AppendLine("MAP_DUMP_END");
            File.WriteAllText(_mapDumpPath, dump.ToString());
        }
        if (_shopDumpPath is not null && state is PlaySession.Playing)
        {
            var dump = new StringBuilder();
            dump.AppendLine(_shop.Dump("live"));
            dump.AppendLine("SHOP_DUMP_END");
            File.WriteAllText(_shopDumpPath, dump.ToString());
        }
        if (_buildDumpPath is not null && state is PlaySession.Playing)
        {
            var dump = new StringBuilder();
            dump.AppendLine(_buildBar.Dump("live"));
            dump.Append("constructs=");
            dump.AppendLine(_session.PlacedConstructs().Count.ToString());
            dump.AppendLine("BUILD_DUMP_END");
            File.WriteAllText(_buildDumpPath, dump.ToString());
        }
        GetTree().Quit();
    }

    private void WriteReport(PlaySession state, string path) =>
        SmokeReport.Write(path, state, new SmokeReportUi(_overlay.IsOpen, _debug is { IsOpen: true }));

    private void MaybeApplyDebugHelper(PlaySession state)
    {
        if (_debugHelper is null)
            return;
        if (state is not PlaySession.Playing playing)
            return;

        bool done = _debugHelper switch
        {
            "intake" => _session.TryTeleportToIntake(),
            "mailbox" => _session.TryTeleportToMailbox(),
            "give-mail" => _session.TryGiveMail(),
            "overlay" => TryOpenLiveOverlay(playing),
            "map" => TryOpenLiveMap(playing),
            "interact" => TryStepInteractSmoke(playing),
            "live-overlay" => TryStepLiveOverlay(playing),
            "leave" => TryStepLeaveSmoke(playing),
            "shop" => TryStepShopSmoke(playing),
            "build" => TryStepBuildSmoke(playing),
            "bike" => _session.TrySpawn(new DebugSpawnId(DebugSpawnKind.Bike, "bike")),
            _ => true,
        };
        if (done)
            _debugHelper = null;
    }

    private bool TryStepInteractSmoke(PlaySession.Playing playing)
    {
        if (playing.Hud.Wallet.Value > 0)
        {
            _holdInteract = false;
            return true;
        }

        if (HasHeldMail(playing))
        {
            _holdInteract = true;
            _session.TryTeleportToMailbox();
            return false;
        }

        if (!_session.TryStockIntake())
            return false;

        _holdInteract = true;
        _session.TryTeleportToIntake();
        return false;
    }

    private bool TryStepLiveOverlay(PlaySession.Playing playing)
    {
        if (HasHeldMail(playing))
        {
            _holdInteract = false;
            return TryOpenLiveOverlay(playing);
        }

        if (!_session.TryStockIntake())
            return false;

        _holdInteract = true;
        _session.TryTeleportToIntake();
        return false;
    }

    private bool TryStepLeaveSmoke(PlaySession.Playing playing)
    {
        OpenPause(playing);
        OnPauseChoice(PauseFrame.LeaveId);
        OnPauseChoice(PauseFrame.ConfirmLeaveId);
        return true;
    }

    private bool TryStepShopSmoke(PlaySession.Playing playing)
    {
        if (playing.Hud.Wallet.Value < 80)
            _session.TryGiveWallet(new Cents(DebugFrame.WalletGrantCents));
        _session.TryBuy("bandage_x3");
        return TryOpenLiveShop(playing);
    }

    private bool TryStepBuildSmoke(PlaySession.Playing playing)
    {
        if (!_session.TryTeleportToIntake())
            return false;
        for (int i = 0; i < 3; i++)
            _session.TrySpawn(new DebugSpawnId(DebugSpawnKind.Item, "log"));
        if (!_session.TryOpenBuild())
            return false;
        _session.Build!.Select("wall_wood");
        return _session.TryPlaceAt(new TileCoord(7, 2));
    }

    private bool HasHeldMail(PlaySession.Playing playing) =>
        HeldMailKind(playing) is not null;

    private MailKindId? HeldMailKind(PlaySession.Playing playing) =>
        HeldMailStack(playing)?.Kind;

    private byte HeldMailDistrict(PlaySession.Playing playing) =>
        HeldMailStack(playing)?.Address.District ?? 0;

    private MailStack? HeldMailStack(PlaySession.Playing playing)
    {
        if (playing.Overlay is not OverlayReplica overlay)
            return null;

        var id = overlay.Hotbar.EntryAt(new Cell((byte)_hotbarSlot, 0));
        if (id.IsNone || !overlay.Hotbar.TryGetEntry(id, out var entry) || entry.Stack is not MailStack mail)
            return null;
        return mail;
    }

    private void SelectHotbar(int slot)
    {
        _hotbarSlot = InputSampler.WrapHotbarSlot(slot);
        _overlay.SelectCell("hotbar", (byte)_hotbarSlot, 0);
        _hud.SelectHotbar(_hotbarSlot);
    }

    private void OnOverlayCellPicked(string grid, byte x, byte y)
    {
        if (grid == "hotbar")
        {
            SelectHotbar(x);
            return;
        }

        if (_session.State is not PlaySession.Playing playing)
            return;
        if (playing.Overlay is not OverlayReplica replica)
            return;

        var source = grid switch
        {
            "inventory" => replica.Inventory,
            "backpack" => replica.Backpack,
            "external" => replica.External,
            _ => null,
        };
        if (source is null)
            return;

        var entry = source.EntryAt(new Cell(x, y));
        if (entry.IsNone)
            return;
        if (_session.TryQuickMove(source.Id, entry, replica.Hotbar.Id))
            SelectHotbar(_hotbarSlot);
    }

    private bool TryOpenLiveOverlay(PlaySession.Playing playing)
    {
        if (playing.Overlay is not OverlayReplica live)
            return false;
        _map.Close();
        BindOverlay(live);
        _overlay.Open();
        return true;
    }

    private bool TryOpenLiveMap(PlaySession.Playing playing)
    {
        if (playing.World is null)
            return false;
        _overlay.Close();
        _shop.Close();
        _map.Open();
        BindMap(playing);
        return true;
    }

    private static TimeSpan WallNow() =>
        TimeSpan.FromTicks((long)Time.GetTicksUsec() * 10);
}
