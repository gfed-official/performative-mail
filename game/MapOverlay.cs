using System.Globalization;
using System.Text;
using Godot;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class MapOverlay : Control
{
    public const string RootPath = "MapRoot";
    public const string TitlePath = "TitleLabel";
    public const string ChipRowPath = "ChipRow";
    public const string PingRowPath = "PingRow";
    public const string CanvasPath = "MapCanvas";
    public const string StatusPath = "StatusLabel";

    public MapLayer Layers { get; private set; } = MapFrame.DefaultLayers;

    public MapFilter Filters { get; private set; }

    public MapPingKind PingKind { get; private set; }

    public MapPingBoard Pings { get; } = new();

    private readonly Dictionary<string, Button> _chips = new();
    private readonly Dictionary<string, Button> _kinds = new();
    private Label _title = null!;
    private Label _status = null!;
    private MapCanvas _canvas = null!;
    private WorldTables? _world;
    private OverlayReplica? _overlay;
    private MapFrame _frame;
    private bool _open;
    private bool _bound;
    private bool _painting;
    private uint _now;

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

    public void Bind(WorldTables? world, OverlayReplica? overlay, uint now)
    {
        BuildChrome();
        _world = world;
        _overlay = overlay;
        _now = now;
        Pings.Expire(now);
        var frame = MapFrame.From(world, overlay, Layers, Filters, Pings.Visible);
        if (_bound && MapFrame.SameDisplay(in _frame, in frame))
            return;
        _frame = frame;
        _bound = true;
        Paint();
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
        PaintChips();
        PaintKinds();
        _canvas.Bind(_frame);
        PaintStatus();
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
        string ping = MapPingText.Label(PingKind);
        int count = _frame.Pings.Count;
        _status.Text = count == 0
            ? "Click the map to ping · " + ping
            : count.ToString(CultureInfo.InvariantCulture) + " ping · " + ping;
    }

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

        _canvas = new MapCanvas { Name = CanvasPath };
        _canvas.PingPicked = OnCanvasPing;
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

    private void OnCanvasPing(TileCoord tile) => TryPlacePing(tile, _now);

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

        private MapFrame _frame;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(520, 340);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public void Bind(in MapFrame frame)
        {
            _frame = frame;
            QueueRedraw();
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse)
                return;
            if (_frame.Width <= 0 || _frame.Height <= 0)
                return;
            var size = Size;
            if (size.X <= 0f || size.Y <= 0f)
                return;
            int x = Math.Clamp((int)(mouse.Position.X * _frame.Width / size.X), 0, _frame.Width - 1);
            int y = Math.Clamp((int)(mouse.Position.Y * _frame.Height / size.Y), 0, _frame.Height - 1);
            PingPicked?.Invoke(new TileCoord(x, y));
            AcceptEvent();
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
            if (_frame.Filters.HasFlag(MapFilter.Routes))
                DrawRoutes(sx, sy);
            DrawPings(sx, sy);
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
