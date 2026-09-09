using Godot;
using PerformativeMail.Client.UI;
using PerformativeMail.Sim.World;

namespace PerformativeMail.Game;

public partial class Hud : Control
{
    public const string ShiftPath = "ShiftLabel";
    public const string PhasePath = "PhaseLabel";
    public const string TimerPath = "TimerLabel";
    public const string WalletPath = "WalletLabel";
    public const string QuotaPath = "QuotaLabel";
    public const string QuotaBarPath = "QuotaBar";
    public const string SurplusPath = "SurplusLabel";
    public const string ComplaintPath = "ComplaintEnvelope";
    public const string HeldPath = "HeldAddress";
    public const string TargetPath = "TargetAddress";
    public const string MatchPath = "MatchMark";
    public const string HotbarPath = "HotbarStrip";
    public const string CompassPath = "CompassStrip";
    public const string CompassDistrictPath = "CompassDistrict";
    public const string CompassFacingPath = "CompassFacing";
    public const string HpBarPath = "HpBar";
    public const string HpPath = "HpLabel";
    public const string WeightIconPath = "WeightIcon";
    public const string WeightPath = "WeightLabel";

    private static readonly Color Amber = new(1f, 0.75f, 0.2f);
    private static readonly Color Red = new(0.9f, 0.2f, 0.2f);
    private static readonly Color Green = new(0.25f, 0.85f, 0.35f);

    private Label _shift = null!;
    private Label _phase = null!;
    private Label _timer = null!;
    private Label _wallet = null!;
    private Label _quota = null!;
    private ProgressBar _quotaBar = null!;
    private Label _surplus = null!;
    private Label _complaint = null!;
    private Label _held = null!;
    private Label _target = null!;
    private Label _match = null!;
    private ProgressBar _hpBar = null!;
    private Label _hp = null!;
    private ColorRect _weightIcon = null!;
    private Label _weight = null!;
    private HBoxContainer _hotbar = null!;
    private readonly ColorRect[] _hotbarSlots = new ColorRect[InputSampler.HotbarSlots];
    private readonly ColorRect[] _hotbarIcons = new ColorRect[InputSampler.HotbarSlots];
    private readonly ColorRect[] _hotbarSwatches = new ColorRect[InputSampler.HotbarSlots];
    private readonly Label[] _hotbarCounts = new Label[InputSampler.HotbarSlots];
    private OverlayGrid _hotbarGrid;
    private int _hotbarSelected = InputSampler.DefaultHotbarSlot;
    private HBoxContainer _compass = null!;
    private Label _compassDistrict = null!;
    private Label _compassFacing = null!;
    private readonly ColorRect[] _compassSlots = new ColorRect[CompassFrame.SlotCount];
    private CompassFrame _compassFrame = CompassFrame.Empty;

    public override void _Ready()
    {
        PlayTheme.Apply(this);
        CacheLabels();
        EnsureStrip();
        EnsureCompass();
    }

    public void Bind(in HudFrame frame)
    {
        CacheLabels();
        SetText(_shift, frame.ShiftLabel);
        SetText(_phase, frame.PhaseLabel);
        SetText(_timer, frame.TimerLabel);
        _timer.Modulate = frame.TimerTone switch
        {
            TimerTone.Amber => Amber,
            TimerTone.Red => Red,
            _ => Colors.White,
        };
        SetText(_wallet, frame.WalletLabel);
        SetText(_quota, frame.QuotaLabel);
        double max = Math.Max(1, frame.QuotaTarget);
        if (_quotaBar.MinValue != 0)
            _quotaBar.MinValue = 0;
        if (_quotaBar.MaxValue != max)
            _quotaBar.MaxValue = max;
        double value = Math.Clamp((double)frame.QuotaEarnings, 0d, max);
        if (_quotaBar.Value != value)
            _quotaBar.Value = value;
        _quotaBar.Modulate = frame.QuotaMet ? Green : Colors.White;
        SetText(_surplus, frame.SurplusLabel);
        SetText(_complaint, frame.ComplaintLabel);
        SetText(_held, frame.HeldAddress);
        SetText(_target, frame.TargetAddress);
        SetText(_match, frame.MatchLabel);
        _match.Modulate = frame.Match switch
        {
            MatchMark.Tick => Green,
            MatchMark.Cross => Red,
            _ => Colors.White,
        };
        if (_hpBar.MinValue != 0)
            _hpBar.MinValue = 0;
        if (_hpBar.MaxValue != 100)
            _hpBar.MaxValue = 100;
        double hp = Math.Clamp((double)frame.HpPct, 0d, 100d);
        if (_hpBar.Value != hp)
            _hpBar.Value = hp;
        _hpBar.Modulate = frame.HpPct switch
        {
            <= 15 => Red,
            <= 60 => Amber,
            _ => Green,
        };
        SetText(_hp, frame.HpLabel);
        if (_weightIcon.Color != PlayTheme.Muted)
            _weightIcon.Color = PlayTheme.Muted;
        SetText(_weight, frame.WeightLabel);
    }

    public void BindCompass(in CompassFrame frame)
    {
        EnsureCompass();
        _compassFrame = frame;
        SetText(_compassFacing, frame.FacingLabel);
        SetText(_compassDistrict, DistrictPalette.HasSwatch(frame.CurrentDistrict)
            ? "D" + frame.CurrentDistrict
            : "");
        if (DistrictPalette.HasSwatch(frame.CurrentDistrict))
            _compassDistrict.Modulate = DistrictSwatch.Of(frame.CurrentDistrict);
        else
            _compassDistrict.Modulate = Colors.White;
        for (int i = 0; i < CompassFrame.SlotCount; i++)
        {
            byte district = i < frame.Slots.Count ? frame.Slots[i] : (byte)0;
            var slot = _compassSlots[i];
            if (DistrictPalette.HasSwatch(district))
            {
                slot.Color = DistrictSwatch.Of(district);
                slot.Modulate = district == frame.CurrentDistrict
                    ? Colors.White
                    : new Color(1f, 1f, 1f, 0.75f);
            }
            else
            {
                slot.Color = PlayTheme.PanelBg;
                slot.Modulate = Colors.White;
            }
        }
    }

    public void BindHotbar(in OverlayGrid hotbar, int selected)
    {
        EnsureStrip();
        _hotbarGrid = hotbar;
        _hotbarSelected = selected;
        for (int i = 0; i < InputSampler.HotbarSlots; i++)
            PaintSlot(i, CellAt(i));
    }

    public void SelectHotbar(int slot)
    {
        EnsureStrip();
        _hotbarSelected = slot;
        for (int i = 0; i < InputSampler.HotbarSlots; i++)
            PaintSelection(i);
    }

    public string Dump(string caseName)
    {
        CacheLabels();
        EnsureStrip();
        EnsureCompass();
        var dump =
            $"HUD_DUMP case={caseName}\n" +
            $"ShiftLabel={_shift.Text}\n" +
            $"PhaseLabel={_phase.Text}\n" +
            $"TimerLabel={_timer.Text}\n" +
            $"WalletLabel={_wallet.Text}\n" +
            $"QuotaLabel={_quota.Text}\n" +
            $"SurplusLabel={_surplus.Text}\n" +
            $"ComplaintLabel={_complaint.Text}\n" +
            $"HeldAddress={_held.Text}\n" +
            $"TargetAddress={_target.Text}\n" +
            $"MatchMark={_match.Text}\n" +
            $"CompassFacing={_compassFrame.FacingLabel}\n" +
            $"CompassDistrict={_compassFrame.CurrentDistrict}\n" +
            $"CompassSlots={_compassFrame.SlotKey}\n" +
            $"HpLabel={_hp.Text}\n" +
            $"WeightLabel={_weight.Text}\n" +
            $"HotbarSelected={_hotbarSelected}\n";
        for (int i = 0; i < InputSampler.HotbarSlots; i++)
        {
            var cell = CellAt(i);
            dump +=
                $"HotbarSlot{i} icon={cell.IconKey} count={cell.CountLabel} address={cell.AddressLabel} selected=" +
                (i == _hotbarSelected ? "1" : "0");
            if (DistrictPalette.HasSwatch(cell.District))
                dump += " district=" + cell.District;
            dump += "\n";
        }

        return dump.TrimEnd('\n');
    }

    private OverlayCell CellAt(int index)
    {
        if (_hotbarGrid.Cells is null || index < 0 || index >= _hotbarGrid.Cells.Count)
            return new OverlayCell("", "", false);
        return _hotbarGrid.Cells[index];
    }

    private static void SetText(Label label, string text)
    {
        if (label.Text != text)
            label.Text = text;
    }

    private void PaintSlot(int index, OverlayCell cell)
    {
        var icon = _hotbarIcons[index];
        var size = HotbarChrome.Size(cell.Icon);
        icon.Visible = cell.Icon != OverlayIcon.Empty;
        icon.Color = HotbarChrome.Fill(cell.Icon);
        icon.CustomMinimumSize = size;
        icon.Size = size;
        icon.Modulate = new Color(1f, 1f, 1f, cell.Opacity);
        CenterIcon(icon, size);
        bool swatch = DistrictPalette.HasSwatch(cell.District);
        _hotbarSwatches[index].Visible = swatch;
        if (swatch)
            _hotbarSwatches[index].Color = DistrictSwatch.Of(cell.District);
        SetText(_hotbarCounts[index], cell.CountLabel);
        _hotbarCounts[index].Modulate = new Color(1f, 1f, 1f, cell.Opacity);
        PaintSelection(index);
    }

    private void PaintSelection(int index)
    {
        var slot = _hotbarSlots[index];
        var color = index == _hotbarSelected ? HotbarChrome.SlotSelected : HotbarChrome.SlotIdle;
        if (slot.Color != color)
            slot.Color = color;
    }

    private static void CenterIcon(ColorRect icon, Vector2 size)
    {
        const float slot = 56f;
        icon.Position = new Vector2((slot - size.X) * 0.5f, 14f + (28f - size.Y) * 0.5f);
    }

    private void EnsureCompass()
    {
        if (_compass is not null)
            return;
        CacheLabels();
        _compassDistrict = GetNode<Label>("%" + CompassDistrictPath);
        _compassFacing = GetNode<Label>("%" + CompassFacingPath);
        _compass = GetNode<HBoxContainer>("%" + CompassPath);
        for (int i = 0; i < CompassFrame.SlotCount; i++)
        {
            if (i == CompassFrame.SlotCount / 2)
                _compass.AddChild(MakeTick());
            var slot = new ColorRect
            {
                Name = "CompassSlot" + i,
                Color = PlayTheme.PanelBg,
                CustomMinimumSize = new Vector2(4, 18),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _compass.AddChild(slot);
            _compassSlots[i] = slot;
        }
    }

    private static ColorRect MakeTick() => new()
    {
        Name = "CompassTick",
        Color = PlayTheme.Body,
        CustomMinimumSize = new Vector2(2, 18),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private void EnsureStrip()
    {
        if (_hotbar is not null)
            return;
        CacheLabels();
        _hotbar = GetNode<HBoxContainer>("%" + HotbarPath);
        for (int i = 0; i < InputSampler.HotbarSlots; i++)
            _hotbar.AddChild(MakeSlot(i));
    }

    private ColorRect MakeSlot(int index)
    {
        var slot = new ColorRect
        {
            Name = "HotbarSlot" + index,
            Color = index == _hotbarSelected ? HotbarChrome.SlotSelected : HotbarChrome.SlotIdle,
            CustomMinimumSize = new Vector2(56, 56),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        var key = new Label
        {
            Text = (index + 1).ToString(),
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        key.Position = new Vector2(4, 2);
        var icon = new ColorRect
        {
            Name = "HotbarIcon" + index,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        var swatch = new ColorRect
        {
            Name = "HotbarSwatch" + index,
            CustomMinimumSize = new Vector2(12, 12),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        swatch.Position = new Vector2(3, 3);
        var count = new Label
        {
            Name = "HotbarCount" + index,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            CustomMinimumSize = new Vector2(52, 52),
        };
        slot.AddChild(key);
        slot.AddChild(icon);
        slot.AddChild(swatch);
        slot.AddChild(count);
        _hotbarSlots[index] = slot;
        _hotbarIcons[index] = icon;
        _hotbarSwatches[index] = swatch;
        _hotbarCounts[index] = count;
        return slot;
    }

    private void CacheLabels()
    {
        if (_shift is not null)
            return;
        PlayTheme.Apply(this);
        _shift = GetNode<Label>("%" + ShiftPath);
        _phase = GetNode<Label>("%" + PhasePath);
        _timer = GetNode<Label>("%" + TimerPath);
        _wallet = GetNode<Label>("%" + WalletPath);
        _quota = GetNode<Label>("%" + QuotaPath);
        _quotaBar = GetNode<ProgressBar>("%" + QuotaBarPath);
        _surplus = GetNode<Label>("%" + SurplusPath);
        _complaint = GetNode<Label>("%" + ComplaintPath);
        _held = GetNode<Label>("%" + HeldPath);
        _target = GetNode<Label>("%" + TargetPath);
        _match = GetNode<Label>("%" + MatchPath);
        _hpBar = GetNode<ProgressBar>("%" + HpBarPath);
        _hp = GetNode<Label>("%" + HpPath);
        _weightIcon = GetNode<ColorRect>("%" + WeightIconPath);
        _weight = GetNode<Label>("%" + WeightPath);
    }
}
