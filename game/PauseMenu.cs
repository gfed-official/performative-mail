using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class PauseMenu : Control
{
    public const string RootPath = "PauseRoot";
    public const string TitlePath = "TitleLabel";
    public const string StatusPath = "StatusLabel";
    public const string BodyPath = "BodyLabel";
    public const string BindsPath = "BindsList";
    public const string OptionsPath = "OptionsList";
    public const string ChoicesPath = "ChoicesColumn";

    public Action<string>? ChoicePicked;

    private Label _title = null!;
    private Label _status = null!;
    private Label _body = null!;
    private ScrollContainer _scroll = null!;
    private VBoxContainer _binds = null!;
    private VBoxContainer _options = null!;
    private VBoxContainer _choices = null!;

    public override void _Ready()
    {
        Name = RootPath;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        BuildChrome();
    }

    public void Bind(in PauseFrame frame, bool open)
    {
        BuildChrome();
        Visible = open;
        MouseFilter = open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        bool screenChanged = _title.Text != frame.Title;
        _title.Text = frame.Title;
        _status.Text = frame.StatusLabel;
        _status.Visible = frame.StatusLabel.Length > 0;
        _body.Text = frame.Body;
        _body.Visible = frame.Body.Length > 0;
        FillBinds(frame.Binds);
        FillOptions(frame.Options);
        SizeScroll(frame.Binds.Count, frame.Options.Count);
        FillChoices(frame.Choices, screenChanged);
    }

    private void BuildChrome()
    {
        if (_title is not null)
            return;

        var dim = new ColorRect
        {
            Color = new Color(0.04f, 0.04f, 0.06f, 0.62f),
            MouseFilter = MouseFilterEnum.Stop,
        };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(dim);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var card = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.16f, 0.96f),
            ContentMarginLeft = 24,
            ContentMarginTop = 20,
            ContentMarginRight = 24,
            ContentMarginBottom = 20,
        };
        card.AddThemeStyleboxOverride("panel", style);
        center.AddChild(card);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 12);
        card.AddChild(column);

        _title = new Label
        {
            Name = TitlePath,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        column.AddChild(_title);

        _status = new Label
        {
            Name = StatusPath,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        column.AddChild(_status);

        _body = new Label
        {
            Name = BodyPath,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(380, 0),
        };
        column.AddChild(_body);

        _scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(392, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        column.AddChild(_scroll);

        var lists = new VBoxContainer();
        lists.AddThemeConstantOverride("separation", 12);
        _scroll.AddChild(lists);

        _binds = new VBoxContainer { Name = BindsPath };
        _binds.AddThemeConstantOverride("separation", 4);
        lists.AddChild(_binds);

        _options = new VBoxContainer { Name = OptionsPath };
        _options.AddThemeConstantOverride("separation", 8);
        lists.AddChild(_options);

        _choices = new VBoxContainer { Name = ChoicesPath };
        _choices.AddThemeConstantOverride("separation", 8);
        column.AddChild(_choices);
    }

    private void FillBinds(IReadOnlyList<PauseBind> binds)
    {
        ClearColumn(_binds);
        _binds.Visible = binds.Count > 0;
        foreach (var bind in binds)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(new Label
            {
                Text = bind.Action,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
            row.AddChild(new Label { Text = bind.Keyboard });
            row.AddChild(new Label { Text = bind.Gamepad });
            _binds.AddChild(row);
        }
    }

    private void FillOptions(IReadOnlyList<PauseOption> options)
    {
        ClearColumn(_options);
        _options.Visible = options.Count > 0;
        foreach (var option in options)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new Label
            {
                Text = option.Label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            });
            row.AddChild(new Label { Text = option.Value });
            AddChoiceButton(row, PauseFrame.OptionDownId(option.Id), "-");
            AddChoiceButton(row, PauseFrame.OptionUpId(option.Id), "+");
            _options.AddChild(row);
        }
    }

    private void SizeScroll(int bindCount, int optionCount)
    {
        int height = 0;
        if (bindCount > 0)
            height = Math.Min(280, bindCount * 28);
        if (optionCount > 0)
            height = Math.Max(height, optionCount * 40);
        _scroll.CustomMinimumSize = new Vector2(392, height);
        _scroll.Visible = height > 0;
    }

    private void FillChoices(IReadOnlyList<PauseChoice> choices, bool grabFocus)
    {
        ClearColumn(_choices);
        Button? first = null;
        foreach (var choice in choices)
        {
            var button = AddChoiceButton(_choices, choice.Id, choice.Label);
            first ??= button;
        }

        if (grabFocus)
            first?.GrabFocus();
    }

    private Button AddChoiceButton(Control parent, string id, string label)
    {
        var button = new Button
        {
            Text = label,
            Name = id,
        };
        string picked = id;
        button.Pressed += () => ChoicePicked?.Invoke(picked);
        parent.AddChild(button);
        return button;
    }

    private static void ClearColumn(VBoxContainer column)
    {
        foreach (var child in column.GetChildren())
            child.Free();
    }
}
