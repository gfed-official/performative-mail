using System.Globalization;
using System.Text;
using Godot;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.Vehicles;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class MapOverlay : Control
{
    public const string RootPath = "MapRoot";
    public const string TitlePath = "TitleLabel";
    public const string ChipRowPath = "ChipRow";
    public const string PingRowPath = "PingRow";
    public const string StopRowPath = "StopRow";
    public const string CanvasPath = "MapCanvas";
    public const string StatusPath = "StatusLabel";

    public MapLayer Layers { get; private set; } = MapFrame.DefaultLayers;

    public MapFilter Filters { get; private set; }

    public MapPingKind PingKind { get; private set; }

    public MapPingBoard Pings { get; } = new();

    public Action<TileCoord>? LivePingRequested;

    public RouteEditor? Editor { get; private set; }

    public bool IsEditing => Editor is not null;

    private readonly Dictionary<string, Button> _chips = new();
    private readonly Dictionary<string, Button> _kinds = new();
    private readonly List<Button> _stopChips = new();
    private Label _title = null!;
    private Label _status = null!;
    private HBoxContainer _kindsRow = null!;
    private HBoxContainer _stopRow = null!;
    private MapCanvas _canvas = null!;
    private WorldTables? _world;
    private OverlayReplica? _overlay;
    private IRouteConsole? _console;
    private WorldTables? _editorWorld;
    private MapFrame _frame;
    private bool _open;
    private bool _bound;
    private bool _painting;
    private uint _now;
    private int _dragStop = -1;
    private int _paintedStopCount = -1;

    public bool IsOpen => _open && Visible;

    public override void _Ready()
    {
        Name = RootPath;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        BuildChrome();
    }

    public void Open()
    {
        Visible = true;
        _open = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Close()
    {
        Visible = false;
        _open = false;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            Open();
    }

    public void Bind(
        WorldTables? world,
        OverlayReplica? overlay,
        uint now,
        IReadOnlyList<MapPing>? livePings = null)
    {
        BuildChrome();
        _world = world;
        _overlay = overlay;
        _now = now;
        Pings.Expire(now);
        RebindEditor();
        var visible = livePings ?? Pings.Visible;
        var frame = MapFrame.From(world, overlay, Layers, Filters, visible);
        if (_bound && MapFrame.SameDisplay(in _frame, in frame) && SamePaintedStops())
            return;
        _frame = frame;
        _bound = true;
        Paint();
    }

    public void OpenRouteEditor(IRouteConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        RebindEditor();
        Open();
        Refresh();
    }

    public void CloseRouteEditor()
    {
        _console = null;
        Editor = null;
        Refresh();
    }

    public bool TryClickStop(TileCoord tile)
    {
        if (Editor is not { } editor)
            return false;
        if (!editor.TryClickTile(tile))
            return false;
        Refresh();
        return true;
    }

    public void MoveStop(int from, int to)
    {
        if (Editor is not { } editor)
            return;
        editor.MoveStop(from, to);
        Refresh();
    }

    public void ToggleChip(string id)
    {
        if (_painting)
            return;
        switch (id)
        {
            case "districts":
                Layers ^= MapLayer.Districts;
                break;
            case "streets":
                Layers ^= MapLayer.Streets;
                break;
            case "mail":
                Filters ^= MapFilter.Mail;
                break;
            case "routes":
                Filters ^= MapFilter.Routes;
                break;
            case "resources":
                Filters ^= MapFilter.Resources;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(id), id, null);
        }

        Refresh();
    }

    public void SelectPingKind(MapPingKind kind)
    {
        if (_painting)
            return;
        PingKind = kind;
        PaintKinds();
        PaintStatus();
    }

    public bool TryPlacePing(TileCoord tile) => TryPlacePing(tile, _now);

    public bool TryPlacePing(TileCoord tile, uint now)
    {
        _now = now;
        if (!Pings.TryPlace(tile, PingKind, now, out _))
            return false;
        Refresh();
        return true;
    }

    public string Dump(string caseName)
    {
        BuildChrome();
        var dump = new StringBuilder();
        dump.Append("MAP_DUMP case=");
        dump.Append(caseName);
        dump.Append('\n');
        dump.Append("visible=");
        dump.Append(IsOpen ? "true" : "false");
        dump.Append('\n');
        dump.Append("width=");
        dump.Append(_frame.Width);
        dump.Append('\n');
        dump.Append("height=");
        dump.Append(_frame.Height);
        dump.Append('\n');
        dump.Append("layers=");
        dump.Append(FormatLayers(_frame.Layers));
        dump.Append('\n');
        dump.Append("filters=");
        dump.Append(FormatFilters(_frame.Filters));
        dump.Append('\n');
        foreach (var chip in _frame.Chips)
        {
            dump.Append("chip.");
            dump.Append(chip.Id);
            dump.Append('=');
            dump.Append(ChipOn(chip.Id) ? "on" : "off");
            dump.Append('\n');
        }

        dump.Append("districts=");
        dump.Append(_frame.Districts.Count);
        dump.Append('\n');
        foreach (var district in _frame.Districts)
        {
            dump.Append("district.");
            dump.Append(district.District);
            dump.Append(" hex=");
            dump.Append(district.Hex);
            dump.Append('\n');
            dump.Append("district.");
            dump.Append(district.District);
            dump.Append(".label=");
            dump.Append(district.Label.X);
            dump.Append(',');
            dump.Append(district.Label.Y);
            dump.Append('\n');
            dump.Append("district.");
            dump.Append(district.District);
            dump.Append(".name=");
            dump.Append(district.Name);
            dump.Append('\n');
        }

        dump.Append("streets=");
        dump.Append(_frame.Streets.Count);
        dump.Append('\n');
        foreach (var street in _frame.Streets)
        {
            dump.Append("street.");
            dump.Append(street.Id);
            dump.Append('=');
            dump.Append(street.Name);
            dump.Append(" d=");
            dump.Append(street.District);
            dump.Append(" hex=");
            dump.Append(street.Hex);
            dump.Append('\n');
        }

        dump.Append("houses=");
        dump.Append(_frame.Houses.Count);
        dump.Append('\n');
        foreach (var house in _frame.Houses)
        {
            dump.Append("house.");
            dump.Append(house.Label);
            dump.Append(" mail=");
            dump.Append(house.HasMail ? "1" : "0");
            dump.Append('\n');
        }

        dump.Append("resources=");
        dump.Append(_frame.Resources.Count);
        dump.Append('\n');
        foreach (var resource in _frame.Resources)
        {
            dump.Append("resource.");
            dump.Append(resource.Label);
            dump.Append('=');
            dump.Append(resource.Tile.X);
            dump.Append(',');
            dump.Append(resource.Tile.Y);
            dump.Append('\n');
        }

        dump.Append("routes=");
        dump.Append(_frame.Routes.Count);
        dump.Append('\n');
        foreach (var route in _frame.Routes)
        {
            dump.Append("route.");
            dump.Append(route.From);
            dump.Append('-');
            dump.Append(route.To);
            dump.Append('\n');
        }

        dump.Append("pings=");
        dump.Append(_frame.Pings.Count);
        dump.Append('\n');
        foreach (var ping in _frame.Pings)
        {
            dump.Append("ping.");
            dump.Append(ping.Id);
            dump.Append(" tile=");
            dump.Append(ping.Tile.X);
            dump.Append(',');
            dump.Append(ping.Tile.Y);
            dump.Append(" kind=");
            dump.Append(ping.Key);
            dump.Append('\n');
        }

        dump.Append("pingKind=");
        dump.Append(MapPingText.Key(PingKind));
        dump.Append('\n');
        dump.Append("editor=");
        dump.Append(IsEditing ? "on" : "off");
        dump.Append('\n');
        dump.Append("stops=");
        dump.Append(Editor is { } live ? live.Stops.Count : 0);
        dump.Append('\n');
        if (Editor is { } editing)
        {
            foreach (var mark in editing.Marks())
            {
                dump.Append("stop.");
                dump.Append(mark.Index);
                dump.Append('=');
                dump.Append(mark.Label);
                dump.Append('\n');
            }

            dump.Append("estimate=");
            dump.Append(editing.TryEstimate(out _, out int seconds)
                ? seconds.ToString(CultureInfo.InvariantCulture) + "s"
                : "");
            dump.Append('\n');
        }

        dump.Append("status=");
        dump.Append(_status.Text);
        dump.Append('\n');
        return dump.ToString();
    }

    private void Refresh()
    {
        _bound = false;
        Bind(_world, _overlay, _now);
    }

    private void Paint()
    {
        _painting = true;
        _title.Text = IsEditing ? "Route editor" : "Map";
        PaintChips();
        PaintKinds();
        PaintStops();
        _kindsRow.Visible = !IsEditing;
        _stopRow.Visible = IsEditing;
        _canvas.Bind(_frame, Editor?.Marks(), IsEditing);
        PaintStatus();
        _paintedStopCount = Editor?.Stops.Count ?? -1;
        Visible = _open;
        _painting = false;
    }

    private void PaintChips()
    {
        foreach (var chip in _frame.Chips)
        {
            if (_chips.TryGetValue(chip.Id, out var button) && button.ButtonPressed != chip.On)
                button.ButtonPressed = chip.On;
        }
    }

    private void PaintKinds()
    {
        foreach (var pair in _kinds)
        {
            bool on = pair.Key == MapPingText.Key(PingKind);
            if (pair.Value.ButtonPressed != on)
                pair.Value.ButtonPressed = on;
        }
    }

    private void PaintStatus()
    {
        if (Editor is { } editor)
        {
            int count = editor.Stops.Count;
            string stops = count == 0
                ? "Click houses or a district label"
                : count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " stop" : " stops");
            _status.Text = editor.TryEstimate(out _, out int seconds)
                ? stops + " · " + seconds.ToString(CultureInfo.InvariantCulture) + "s round-trip"
                : stops;
            return;
        }

        string ping = MapPingText.Label(PingKind);
        int pings = _frame.Pings.Count;
        _status.Text = pings == 0
            ? "Click the map to ping · " + ping
            : pings.ToString(CultureInfo.InvariantCulture) + " ping · " + ping;
    }

    private void PaintStops()
    {
        foreach (var chip in _stopChips)
            chip.QueueFree();
        _stopChips.Clear();
        if (Editor is not { } editor)
            return;
        foreach (var mark in editor.Marks())
        {
            var button = new Button
            {
                Name = "Stop_" + mark.Index.ToString(CultureInfo.InvariantCulture),
                Text = (mark.Index + 1).ToString(CultureInfo.InvariantCulture) + " " + mark.Label,
            };
            int captured = mark.Index;
            button.GuiInput += @event => OnStopChipInput(captured, @event);
            _stopRow.AddChild(button);
            _stopChips.Add(button);
        }
    }

    private void OnStopChipInput(int index, InputEvent @event)
    {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
            return;
        if (mouse.Pressed)
        {
            _dragStop = index;
            return;
        }

        if (_dragStop >= 0 && _dragStop != index)
            MoveStop(_dragStop, index);
        _dragStop = -1;
    }

    private void RebindEditor()
    {
        if (_console is null)
        {
            Editor = null;
            _editorWorld = null;
            return;
        }

        if (_world is null)
            return;
        if (Editor is not null
            && ReferenceEquals(Editor.Console, _console)
            && ReferenceEquals(_editorWorld, _world))
            return;

        Editor = new RouteEditor(_console, _world);
        _editorWorld = _world;
    }

    private bool SamePaintedStops() =>
        (Editor?.Stops.Count ?? -1) == _paintedStopCount;

    private bool ChipOn(string id) =>
        _chips.TryGetValue(id, out var button) && button.ButtonPressed;

    private void BuildChrome()
    {
        if (_title is not null)
            return;

        PlayTheme.Apply(this);

        var dim = new ColorRect
        {
            Color = new Color(0.04f, 0.04f, 0.06f, 0.55f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 48);
        margin.AddThemeConstantOverride("margin_top", 72);
        margin.AddThemeConstantOverride("margin_right", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        var card = new PanelContainer();
        margin.AddChild(card);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        card.AddChild(column);

        _title = new Label
        {
            Name = TitlePath,
            Text = "Map",
        };
        column.AddChild(_title);

        var chips = new HBoxContainer { Name = ChipRowPath };
        chips.AddThemeConstantOverride("separation", 8);
        column.AddChild(chips);
        AddChip(chips, "districts", "Districts");
        AddChip(chips, "streets", "Streets");
        AddChip(chips, "mail", "Mail");
        AddChip(chips, "routes", "Routes");
        AddChip(chips, "resources", "Resources");

        var kinds = new HBoxContainer { Name = PingRowPath };
        kinds.AddThemeConstantOverride("separation", 8);
        column.AddChild(kinds);
        AddKind(kinds, MapPingKind.Default);
        AddKind(kinds, MapPingKind.DeliverHere);
        AddKind(kinds, MapPingKind.BuildHere);
        AddKind(kinds, MapPingKind.Danger);
        AddKind(kinds, MapPingKind.NeedMaterials);
        _kindsRow = kinds;

        _stopRow = new HBoxContainer { Name = StopRowPath, Visible = false };
        _stopRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(_stopRow);

        _canvas = new MapCanvas { Name = CanvasPath };
        _canvas.PingPicked = OnCanvasPing;
        _canvas.StopMoved = MoveStop;
        column.AddChild(_canvas);

        _status = new Label { Name = StatusPath };
        PlayTheme.ApplyMuted(_status);
        column.AddChild(_status);
        _frame = MapFrame.Empty(Layers, Filters);
        Paint();
    }

    private void AddChip(HBoxContainer row, string id, string label)
    {
        var button = new Button
        {
            Name = "Chip_" + id,
            Text = label,
            ToggleMode = true,
        };
        string captured = id;
        button.Pressed += () => Callable.From(() => ToggleChip(captured)).CallDeferred();
        row.AddChild(button);
        _chips[id] = button;
    }

    private void AddKind(HBoxContainer row, MapPingKind kind)
    {
        string key = MapPingText.Key(kind);
        var button = new Button
        {
            Name = "Ping_" + key,
            Text = MapPingText.Label(kind),
            ToggleMode = true,
        };
        var captured = kind;
        button.Pressed += () => Callable.From(() => SelectPingKind(captured)).CallDeferred();
        row.AddChild(button);
        _kinds[key] = button;
    }

    private void OnCanvasPing(TileCoord tile)
    {
        if (IsEditing)
        {
            TryClickStop(tile);
            return;
        }

        if (LivePingRequested is { } live)
            live(tile);
        else
            TryPlacePing(tile, _now);
    }

    private static string FormatLayers(MapLayer layers)
    {
        if (layers == MapLayer.None)
            return "";
        var parts = new List<string>();
        if (layers.HasFlag(MapLayer.Districts))
            parts.Add("districts");
        if (layers.HasFlag(MapLayer.Streets))
            parts.Add("streets");
        return string.Join(",", parts);
    }

    private static string FormatFilters(MapFilter filters)
    {
        if (filters == MapFilter.None)
            return "";
        var parts = new List<string>();
        if (filters.HasFlag(MapFilter.Mail))
            parts.Add("mail");
        if (filters.HasFlag(MapFilter.Routes))
            parts.Add("routes");
        if (filters.HasFlag(MapFilter.Resources))
            parts.Add("resources");
        return string.Join(",", parts);
    }

    private sealed partial class MapCanvas : Control
    {
        public Action<TileCoord>? PingPicked;

        public Action<int, int>? StopMoved;

        private MapFrame _frame;
        private IReadOnlyList<RouteEditorStop> _stops = Array.Empty<RouteEditorStop>();
        private bool _editing;
        private int _dragStop = -1;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(520, 340);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public void Bind(in MapFrame frame, IReadOnlyList<RouteEditorStop>? stops = null, bool editing = false)
        {
            _frame = frame;
            _stops = stops ?? Array.Empty<RouteEditorStop>();
            _editing = editing;
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
                return;
            if (_frame.Width <= 0 || _frame.Height <= 0)
                return;
            var size = Size;
            if (size.X <= 0f || size.Y <= 0f)
                return;
            if (!TryTileAt(mouse.Position, out var tile))
                return;

            if (_editing && TryHitStop(tile, out int index))
            {
                if (mouse.Pressed)
                    _dragStop = index;
                else if (_dragStop >= 0 && _dragStop != index)
                {
                    StopMoved?.Invoke(_dragStop, index);
                    _dragStop = -1;
                }
                else
                    _dragStop = -1;
                AcceptEvent();
                return;
            }

            if (!mouse.Pressed)
            {
                _dragStop = -1;
                return;
            }

            PingPicked?.Invoke(tile);
            AcceptEvent();
        }

        private bool TryTileAt(Vector2 position, out TileCoord tile)
        {
            var size = Size;
            int x = Math.Clamp((int)(position.X * _frame.Width / size.X), 0, _frame.Width - 1);
            int y = Math.Clamp((int)(position.Y * _frame.Height / size.Y), 0, _frame.Height - 1);
            tile = new TileCoord(x, y);
            return true;
        }

        private bool TryHitStop(TileCoord tile, out int index)
        {
            for (int i = 0; i < _stops.Count; i++)
            {
                int dx = tile.X - _stops[i].Tile.X;
                if (dx < 0) dx = -dx;
                int dy = tile.Y - _stops[i].Tile.Y;
                if (dy < 0) dy = -dy;
                if (dx <= 1 && dy <= 1)
                {
                    index = _stops[i].Index;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        public override void _Draw()
        {
            var size = Size;
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.08f, 0.1f, 0.13f, 0.96f));
            if (_frame.Width <= 0 || _frame.Height <= 0)
                return;

            float sx = size.X / _frame.Width;
            float sy = size.Y / _frame.Height;
            if (_frame.Layers.HasFlag(MapLayer.Districts))
                DrawDistricts(sx, sy);
            if (_frame.Layers.HasFlag(MapLayer.Streets))
                DrawStreets(sx, sy);
            DrawHouses(sx, sy);
            if (_frame.Filters.HasFlag(MapFilter.Resources))
                DrawResources(sx, sy);
            if (_frame.Filters.HasFlag(MapFilter.Routes) || _editing)
                DrawRoutes(sx, sy);
            DrawPings(sx, sy);
            DrawDistrictLabels(sx, sy);
            DrawStops(sx, sy);
        }

        private void DrawDistricts(float sx, float sy)
        {
            var raster = _frame.Raster;
            if (raster.CellsX <= 0 || raster.CellsY <= 0)
                return;
            float cw = sx * raster.CellTiles;
            float ch = sy * raster.CellTiles;
            for (int cy = 0; cy < raster.CellsY; cy++)
            {
                for (int cx = 0; cx < raster.CellsX; cx++)
                {
                    byte district = raster[cx, cy];
                    if (!DistrictPalette.HasSwatch(district))
                        continue;
                    var color = DistrictSwatch.Of(district);
                    color.A = 0.42f;
                    DrawRect(new Rect2(cx * cw, cy * ch, cw, ch), color);
                }
            }
        }

        private void DrawStreets(float sx, float sy)
        {
            foreach (var street in _frame.Streets)
            {
                var color = DistrictSwatch.Of(street.District);
                color.A = 0.9f;
                foreach (var tile in street.Tiles)
                {
                    DrawRect(
                        new Rect2(tile.X * sx, tile.Y * sy, MathF.Max(sx, 2f), MathF.Max(sy, 2f)),
                        color);
                }
            }
        }

        private void DrawHouses(float sx, float sy)
        {
            bool mail = _frame.Filters.HasFlag(MapFilter.Mail);
            foreach (var house in _frame.Houses)
            {
                var color = house.HasMail && mail
                    ? new Color(1f, 0.92f, 0.35f, 0.95f)
                    : new Color(0.88f, 0.81f, 0.66f, 0.85f);
                DrawRect(
                    new Rect2(house.Lot.X * sx, house.Lot.Y * sy, house.Size.X * sx, house.Size.Y * sy),
                    color);
            }
        }

        private void DrawResources(float sx, float sy)
        {
            foreach (var resource in _frame.Resources)
            {
                float x = (resource.Tile.X + 0.5f) * sx;
                float y = (resource.Tile.Y + 0.5f) * sy;
                DrawCircle(new Vector2(x, y), MathF.Max(4f, MathF.Min(sx, sy) * 0.4f), new Color(0.25f, 0.85f, 0.35f, 0.95f));
            }
        }

        private void DrawRoutes(float sx, float sy)
        {
            var color = PlayTheme.Primary;
            foreach (var route in _frame.Routes)
            {
                var from = new Vector2((route.FromTile.X + 0.5f) * sx, (route.FromTile.Y + 0.5f) * sy);
                var to = new Vector2((route.ToTile.X + 0.5f) * sx, (route.ToTile.Y + 0.5f) * sy);
                DrawLine(from, to, color, 3f);
            }
        }

        private void DrawPings(float sx, float sy)
        {
            foreach (var ping in _frame.Pings)
            {
                float x = (ping.Tile.X + 0.5f) * sx;
                float y = (ping.Tile.Y + 0.5f) * sy;
                DrawCircle(new Vector2(x, y), MathF.Max(6f, MathF.Min(sx, sy) * 0.55f), PingColor(ping.Kind));
            }
        }

        private void DrawDistrictLabels(float sx, float sy)
        {
            if (!_frame.Layers.HasFlag(MapLayer.Districts) && !_editing)
                return;
            var font = ThemeDB.FallbackFont;
            int size = Math.Max(12, (int)MathF.Min(sx, sy) * 2);
            foreach (var district in _frame.Districts)
            {
                var color = DistrictSwatch.Of(district.District);
                var pos = new Vector2(district.Label.X * sx, district.Label.Y * sy);
                DrawRect(new Rect2(pos, new Vector2(MathF.Max(sx * 2f, 16f), MathF.Max(sy, 12f))), new Color(0.06f, 0.07f, 0.09f, 0.72f));
                DrawString(font, pos, district.Name, HorizontalAlignment.Left, -1, size, color);
            }
        }

        private void DrawStops(float sx, float sy)
        {
            if (_stops.Count == 0)
                return;
            var font = ThemeDB.FallbackFont;
            int size = Math.Max(12, (int)MathF.Min(sx, sy) * 2);
            for (int i = 0; i < _stops.Count; i++)
            {
                var stop = _stops[i];
                float x = (stop.Tile.X + 0.5f) * sx;
                float y = (stop.Tile.Y + 0.5f) * sy;
                DrawCircle(new Vector2(x, y), MathF.Max(8f, MathF.Min(sx, sy) * 0.65f), PlayTheme.Primary);
                DrawString(
                    font,
                    new Vector2(x - 4f, y - size * 0.4f),
                    (stop.Index + 1).ToString(CultureInfo.InvariantCulture),
                    HorizontalAlignment.Left,
                    -1,
                    size,
                    PlayTheme.Body);
            }
        }

        private static Color PingColor(MapPingKind kind) => kind switch
        {
            MapPingKind.Default => PlayTheme.Primary,
            MapPingKind.DeliverHere => new Color(0.25f, 0.85f, 0.35f),
            MapPingKind.BuildHere => new Color(0.95f, 0.82f, 0.29f),
            MapPingKind.Danger => PlayTheme.Danger,
            MapPingKind.NeedMaterials => new Color(0.61f, 0.35f, 0.71f),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
