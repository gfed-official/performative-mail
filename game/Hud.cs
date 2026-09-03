using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class Hud : Control
{
    public const string ShiftPath = "ShiftLabel";
    public const string PhasePath = "PhaseLabel";
    public const string TimerPath = "TimerLabel";
    public const string WalletPath = "WalletLabel";
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
    private Label _held = null!;
    private Label _target = null!;
    private Label _match = null!;

    public override void _Ready() => CacheLabels();

    public void Bind(in HudFrame frame)
    {
        CacheLabels();
        _shift.Text = frame.ShiftLabel;
        _phase.Text = frame.PhaseLabel;
        _timer.Text = frame.TimerLabel;
        _timer.Modulate = frame.TimerTone switch
        {
            TimerTone.Amber => Amber,
            TimerTone.Red => Red,
            _ => Colors.White,
        };
        _wallet.Text = frame.WalletLabel;
        _held.Text = frame.HeldAddress;
        _target.Text = frame.TargetAddress;
        _match.Text = frame.MatchLabel;
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
            $"HeldAddress={_held.Text}\n" +
            $"TargetAddress={_target.Text}\n" +
            $"MatchMark={_match.Text}";
    }

    private void CacheLabels()
    {
        if (_shift is not null)
            return;
        _shift = GetNode<Label>("%" + ShiftPath);
        _phase = GetNode<Label>("%" + PhasePath);
        _timer = GetNode<Label>("%" + TimerPath);
        _wallet = GetNode<Label>("%" + WalletPath);
        _held = GetNode<Label>("%" + HeldPath);
        _target = GetNode<Label>("%" + TargetPath);
        _match = GetNode<Label>("%" + MatchPath);
    }
}
