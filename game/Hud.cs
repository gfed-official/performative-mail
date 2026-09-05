using Godot;
using PerformativeMail.Client.UI;

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

    public override void _Ready()
    {
        PlayTheme.Apply(this);
        CacheLabels();
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
    }

    public string Dump(string caseName)
    {
        CacheLabels();
        return
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
            $"MatchMark={_match.Text}";
    }

    private static void SetText(Label label, string text)
    {
        if (label.Text != text)
            label.Text = text;
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
    }
}
