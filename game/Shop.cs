using System.Text;
using Godot;
using PerformativeMail.Client.UI;

namespace PerformativeMail.Game;

public partial class Shop : Control
{
    public const string TitlePath = "TitleLabel";
    public const string WalletPath = "WalletLabel";
    public const string PhasePath = "PhaseLabel";
    public const string OffersPath = "Offers";
    public const string ClosePath = "CloseButton";

    public Action<string>? BuyPressed;

    private Label _title = null!;
    private Label _wallet = null!;
    private Label _phase = null!;
    private VBoxContainer _offers = null!;
    private Button _close = null!;
    private ShopFrame _frame;
    private bool _open;

    public bool IsOpen => _open && Visible;

    public override void _Ready()
    {
        PlayTheme.Apply(this);
        CacheNodes();
    }

    public void Open()
    {
        CacheNodes();
        Visible = true;
        _open = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void Close()
    {
        CacheNodes();
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

    public void Bind(in ShopFrame frame)
    {
        CacheNodes();
        _frame = frame;
        _wallet.Text = frame.WalletLabel;
        _phase.Text = frame.PhaseLabel;
        ClearOffers();
        for (int i = 0; i < frame.Rows.Count; i++)
            _offers.AddChild(MakeRow(frame.Rows[i]));
        Visible = _open;
        MouseFilter = _open ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    public string Dump(string caseName)
    {
        CacheNodes();
        var dump = new StringBuilder();
        dump.Append("SHOP_DUMP case=");
        dump.Append(caseName);
        dump.Append('\n');
        dump.Append("visible=");
        dump.Append(IsOpen ? "true" : "false");
        dump.Append('\n');
        dump.Append("WalletLabel=");
        dump.Append(_wallet.Text);
        dump.Append('\n');
        dump.Append("PhaseLabel=");
        dump.Append(_phase.Text);
        dump.Append('\n');
        dump.Append("OfferCount=");
        dump.Append(_frame.Rows is null ? 0 : _frame.Rows.Count);
        dump.Append('\n');
        if (_frame.Rows is not null)
        {
            for (int i = 0; i < _frame.Rows.Count; i++)
            {
                var row = _frame.Rows[i];
                dump.Append("Offer.");
                dump.Append(row.Id);
                dump.Append('=');
                dump.Append(row.NameLabel);
                dump.Append('|');
                dump.Append(row.PriceLabel);
                dump.Append('|');
                dump.Append(row.TagLabel);
                dump.Append('|');
                dump.Append(row.CanBuy ? "can" : "locked");
                dump.Append('\n');
            }
        }

        return dump.ToString().TrimEnd('\n');
    }

    private Control MakeRow(in ShopRowFrame row)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 12);

        var name = new Label
        {
            Text = row.NameLabel,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        box.AddChild(name);

        var price = new Label { Text = row.PriceLabel };
        box.AddChild(price);

        var tag = new Label { Text = row.TagLabel };
        PlayTheme.ApplyMuted(tag);
        box.AddChild(tag);

        var buy = new Button
        {
            Name = "Buy_" + row.Id,
            Text = "Buy",
            Disabled = !row.CanBuy,
        };
        string id = row.Id;
        buy.Pressed += () => BuyPressed?.Invoke(id);
        box.AddChild(buy);
        return box;
    }

    private void ClearOffers()
    {
        while (_offers.GetChildCount() > 0)
            _offers.GetChild(0).Free();
    }

    private void CacheNodes()
    {
        if (_title is not null)
            return;
        PlayTheme.Apply(this);
        _title = GetNode<Label>("%" + TitlePath);
        _wallet = GetNode<Label>("%" + WalletPath);
        _phase = GetNode<Label>("%" + PhasePath);
        PlayTheme.ApplyMuted(_wallet);
        PlayTheme.ApplyMuted(_phase);
        _offers = GetNode<VBoxContainer>("%" + OffersPath);
        _close = GetNode<Button>("%" + ClosePath);
        _close.Pressed += Close;
    }
}
