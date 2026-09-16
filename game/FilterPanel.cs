using System.Text;
using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class FilterPanel : Control
{
    public const string RootPath = "FilterPanel";
    public const string ChipPath = "ChipRow";
    public const string UnmatchedPath = "UnmatchedLabel";

    private HBoxContainer _chips = null!;
    private Label _unmatched = null!;
    private FilterPanelFrame _frame = new(false, Array.Empty<FilterChip>(), 0);
    public Action<string>? ChipPicked;

    public bool IsOpen => Visible && _frame.Open;

    public override void _Ready()
    {
        Name = RootPath;
        SetAnchorsPreset(LayoutPreset.TopLeft);
        OffsetLeft = 16;
        OffsetTop = 80;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        PlayTheme.Apply(this);
        _ = Rows();
    }

    public void Bind(in FilterPanelFrame frame)
    {
        var (chips, unmatched) = Rows();
        _frame = frame;
        Visible = frame.Open;
        ClearRow(chips);
        for (int i = 0; i < frame.Chips.Count; i++)
        {
            var chip = frame.Chips[i];
            chips.AddChild(MakeButton(chip.Label, chip.Selected, () => ChipPicked?.Invoke(chip.Id)));
        }

        unmatched.Text = frame.UnmatchedCount.ToString();
    }

    public string Dump(string caseName)
    {
        _ = Rows();
        var dump = new StringBuilder();
        dump.Append("FILTER_DUMP case=");
        dump.Append(caseName);
        dump.Append('\n');
        dump.Append("open=");
        dump.Append(IsOpen ? "true" : "false");
        dump.Append('\n');
        dump.Append("unmatched=");
        dump.Append(_frame.UnmatchedCount);
        dump.Append('\n');
        for (int i = 0; i < _frame.Chips.Count; i++)
        {
            dump.Append("chip_");
            dump.Append(i);
            dump.Append('=');
            dump.Append(_frame.Chips[i].Id);
            dump.Append('\n');
        }

        dump.Append("UnmatchedLabel=");
        dump.Append(_unmatched.Text);
        return dump.ToString();
    }

    private (HBoxContainer Chips, Label Unmatched) Rows()
    {
        if (_chips is not null)
            return (_chips, _unmatched);

        PlayTheme.Apply(this);
        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.TopLeft);
        column.AddThemeConstantOverride("separation", 6);
        column.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(column);

        _chips = new HBoxContainer { Name = ChipPath };
        _chips.AddThemeConstantOverride("separation", 6);
        _chips.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(_chips);

        _unmatched = new Label { Name = UnmatchedPath, MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(_unmatched);
        return (_chips, _unmatched);
    }

    private static Button MakeButton(string text, bool selected, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            ButtonPressed = selected,
            MouseFilter = MouseFilterEnum.Stop,
        };
        button.Pressed += () => pressed();
        return button;
    }

    private static void ClearRow(HBoxContainer row)
    {
        while (row.GetChildCount() > 0)
        {
            var child = row.GetChild(0);
            row.RemoveChild(child);
            child.QueueFree();
        }
    }
}
