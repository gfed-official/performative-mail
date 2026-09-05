using System.Globalization;
using System.Text;
using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class InventoryOverlay : Control
{
    public const string RootPath = "OverlayRoot";
    public const string LeftPath = "LeftColumn";
    public const string RightPath = "RightColumn";

    private readonly Dictionary<string, Label> _cells = new();
    private VBoxContainer _left = null!;
    private VBoxContainer _right = null!;
    private OverlayFrame _frame;
    private bool _open;

    public bool IsOpen => _open && Visible;

    public override void _Ready()
    {
        Name = RootPath;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        _ = Columns();
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

    public void Bind(in OverlayFrame frame)
    {
        var (left, right) = Columns();
        _frame = frame;
        ClearColumn(left);
        ClearColumn(right);
        _cells.Clear();
        AddGrid(left, frame.Hotbar);
        AddGrid(left, frame.Inventory);
        if (frame.Backpack is { } pack)
            AddGrid(left, pack);
        if (frame.External is { } ext)
            AddGrid(right, ext);
        Visible = _open;
    }

    public string Dump(string caseName)
    {
        _ = Columns();
        var dump = new StringBuilder();
        dump.Append("OVERLAY_DUMP case=");
        dump.Append(caseName);
        dump.Append('\n');
        dump.Append("visible=");
        dump.Append(IsOpen ? "true" : "false");
        dump.Append('\n');
        WriteGrid(dump, _frame.Hotbar);
        WriteGrid(dump, _frame.Inventory);
        if (_frame.Backpack is { } pack)
            WriteGrid(dump, pack);
        if (_frame.External is { } ext)
            WriteGrid(dump, ext);
        foreach (var pair in _cells)
        {
            if (!GodotObject.IsInstanceValid(pair.Value))
                continue;
            dump.Append(pair.Key);
            dump.Append(" text=");
            dump.Append(pair.Value.Text);
            dump.Append(" opacity=");
            dump.Append(pair.Value.Modulate.A.ToString("0.0", CultureInfo.InvariantCulture));
            dump.Append('\n');
        }

        return dump.ToString();
    }

    private (VBoxContainer Left, VBoxContainer Right) Columns()
    {
        if (_left is { } left && _right is { } right)
            return (left, right);

        var dim = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.08f, 0.35f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 48);
        margin.AddThemeConstantOverride("margin_top", 120);
        margin.AddThemeConstantOverride("margin_right", 48);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 32);
        margin.AddChild(row);

        left = new VBoxContainer { Name = LeftPath, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        left.AddThemeConstantOverride("separation", 16);
        row.AddChild(left);

        right = new VBoxContainer { Name = RightPath, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 16);
        row.AddChild(right);

        _left = left;
        _right = right;
        return (left, right);
    }

    private void AddGrid(VBoxContainer column, OverlayGrid grid)
    {
        var block = new VBoxContainer();
        block.AddThemeConstantOverride("separation", 4);
        column.AddChild(block);

        block.AddChild(new Label { Text = grid.Name });

        var cells = new Godot.GridContainer { Columns = grid.Cols };
        cells.AddThemeConstantOverride("h_separation", 4);
        cells.AddThemeConstantOverride("v_separation", 4);
        block.AddChild(cells);
        if (grid.Cells is null || grid.Cells.Count == 0)
            return;

        for (byte y = 0; y < grid.Rows; y++)
        {
            for (byte x = 0; x < grid.Cols; x++)
            {
                int i = y * grid.Cols + x;
                if (i >= grid.Cells.Count)
                    return;
                var cell = grid.Cells[i];
                var label = new Label
                {
                    Name = CellName(grid.Name, x, y),
                    Text = cell.Text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    CustomMinimumSize = new Vector2(72, 40),
                    Modulate = new Color(1f, 1f, 1f, cell.Opacity),
                };
                var slot = new ColorRect
                {
                    Color = new Color(0.16f, 0.18f, 0.22f, 0.92f),
                    CustomMinimumSize = new Vector2(76, 44),
                };
                slot.AddChild(label);
                label.SetAnchorsPreset(LayoutPreset.FullRect);
                cells.AddChild(slot);
                _cells[label.Name] = label;
            }
        }
    }

    private static void WriteGrid(StringBuilder dump, OverlayGrid grid)
    {
        dump.Append(grid.Name);
        dump.Append(" cols=");
        dump.Append(grid.Cols);
        dump.Append(" rows=");
        dump.Append(grid.Rows);
        dump.Append('\n');
        if (grid.Cells is null || grid.Cells.Count == 0)
            return;
        for (byte y = 0; y < grid.Rows; y++)
        {
            for (byte x = 0; x < grid.Cols; x++)
            {
                int i = y * grid.Cols + x;
                if (i >= grid.Cells.Count)
                    return;
                var cell = grid.Cells[i];
                if (cell.Text.Length == 0 && !cell.Pending)
                    continue;
                dump.Append(grid.Name);
                dump.Append('[');
                dump.Append(x);
                dump.Append(',');
                dump.Append(y);
                dump.Append("] count=");
                dump.Append(cell.CountLabel);
                dump.Append(" address=");
                dump.Append(cell.AddressLabel);
                dump.Append(" pending=");
                dump.Append(cell.Pending ? "1" : "0");
                dump.Append(" opacity=");
                dump.Append(cell.Opacity.ToString("0.0", CultureInfo.InvariantCulture));
                dump.Append('\n');
            }
        }
    }

    private static string CellName(string grid, byte x, byte y) => grid + "_" + x + "_" + y;

    private static void ClearColumn(VBoxContainer column)
    {
        while (column.GetChildCount() > 0)
            column.GetChild(0).Free();
    }
}
