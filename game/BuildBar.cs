using System.Text;
using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class BuildBar : Control
{
    public const string RootPath = "BuildBar";
    public const string CategoryPath = "CategoryRow";
    public const string ChoicePath = "ChoiceRow";
    public const string SelectedPath = "SelectedLabel";
    public const string ReasonPath = "ReasonLabel";

    private HBoxContainer _categories = null!;
    private HBoxContainer _choices = null!;
    private Label _selected = null!;
    private Label _reason = null!;
    private BuildFrame _frame;
    public Action<BuildCategory>? CategoryPicked;
    public Action<string>? ChoicePicked;

    public bool IsOpen => Visible && _frame.Open;

    public override void _Ready()
    {
        Name = RootPath;
        SetAnchorsPreset(LayoutPreset.BottomWide);
        OffsetTop = -120;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        PlayTheme.Apply(this);
        _ = Rows();
    }

    public void Bind(in BuildFrame frame)
    {
        var (categories, choices, selected, reason) = Rows();
        _frame = frame;
        Visible = frame.Open;
        ClearRow(categories);
        ClearRow(choices);
        for (int i = 0; i < frame.Categories.Count; i++)
        {
            var tab = frame.Categories[i];
            categories.AddChild(MakeButton(tab.Label, tab.Selected, () => CategoryPicked?.Invoke(tab.Category)));
        }

        for (int i = 0; i < frame.Choices.Count; i++)
        {
            var choice = frame.Choices[i];
            choices.AddChild(MakeButton(choice.Name, choice.Selected, () => ChoicePicked?.Invoke(choice.Id)));
        }

        selected.Text = frame.SelectedName.Length == 0
            ? ""
            : frame.SelectedName + "  " + frame.Facing;
        reason.Text = frame.Reason;
        reason.Modulate = frame.Valid ? PlayTheme.Body : PlayTheme.Danger;
    }

    public string Dump(string caseName)
    {
        _ = Rows();
        var dump = new StringBuilder();
        dump.Append("BUILD_DUMP case=");
        dump.Append(caseName);
        dump.Append('\n');
        dump.Append("open=");
        dump.Append(IsOpen ? "true" : "false");
        dump.Append('\n');
        dump.Append("category=");
        dump.Append(BuildCategories.Label(_frame.Category));
        dump.Append('\n');
        dump.Append("selected=");
        dump.Append(_frame.SelectedId);
        dump.Append('\n');
        dump.Append("selectedName=");
        dump.Append(_frame.SelectedName);
        dump.Append('\n');
        dump.Append("facing=");
        dump.Append(_frame.Facing);
        dump.Append('\n');
        dump.Append("valid=");
        dump.Append(_frame.Valid ? "true" : "false");
        dump.Append('\n');
        dump.Append("reason=");
        dump.Append(_frame.Reason);
        dump.Append('\n');
        for (int i = 0; i < _frame.Categories.Count; i++)
        {
            dump.Append("category_");
            dump.Append(i);
            dump.Append('=');
            dump.Append(_frame.Categories[i].Label);
            dump.Append('\n');
        }

        for (int i = 0; i < _frame.Choices.Count; i++)
        {
            dump.Append("choice_");
            dump.Append(i);
            dump.Append('=');
            dump.Append(_frame.Choices[i].Id);
            dump.Append('\n');
        }

        dump.Append("SelectedLabel=");
        dump.Append(_selected.Text);
        dump.Append('\n');
        dump.Append("ReasonLabel=");
        dump.Append(_reason.Text);
        return dump.ToString();
    }

    private (HBoxContainer Categories, HBoxContainer Choices, Label Selected, Label Reason) Rows()
    {
        if (_categories is not null)
            return (_categories, _choices, _selected, _reason);

        PlayTheme.Apply(this);
        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        column.AddThemeConstantOverride("separation", 6);
        column.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(column);

        _categories = new HBoxContainer { Name = CategoryPath };
        _categories.AddThemeConstantOverride("separation", 6);
        _categories.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(_categories);

        _choices = new HBoxContainer { Name = ChoicePath };
        _choices.AddThemeConstantOverride("separation", 6);
        _choices.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(_choices);

        _selected = new Label { Name = SelectedPath, MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(_selected);
        _reason = new Label { Name = ReasonPath, MouseFilter = MouseFilterEnum.Ignore };
        column.AddChild(_reason);
        return (_categories, _choices, _selected, _reason);
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
