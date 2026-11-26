using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Horizon.Marketplace;
using Content.Shared._NF.Bank;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// A single marketplace listing: item picture, price, seller details and the buy / withdraw controls.
/// </summary>
public sealed class MarketplaceListingCard : PanelContainer
{
    private readonly MarketplaceTheme _theme;
    private Color CardBackground => _theme.CardBackground;
    private Color CardBorder => _theme.CardBorder;
    private Color OwnBorder => _theme.OwnBorder;
    private string MutedColor => _theme.Muted;
    private string PriceColor => _theme.Price;
    private string AccentColor => _theme.Accent;

    public event Action<int, int, int>? OnBuy;
    public event Action<int>? OnWithdraw;
    public event Action<int, int>? OnAmountChanged;

    private readonly MarketplaceListingData _data;
    private readonly RichTextLabel _totalLabel;
    private readonly Button? _actionButton;
    private readonly bool _readOnly;
    private readonly int _balance;
    private int _amount = 1;

    public MarketplaceListingCard(MarketplaceTheme theme,
        MarketplaceListingData data,
        int balance,
        bool hasAccount,
        int initialAmount = 1,
        bool readOnly = false)
    {
        _theme = theme;
        _readOnly = readOnly;
        _data = data;
        _amount = Math.Clamp(initialAmount, 1, Math.Max(1, data.Count));
        _balance = balance;
        HorizontalExpand = true;
        Margin = new Thickness(4);

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = CardBackground,
            BorderColor = data.IsOwn ? OwnBorder : CardBorder,
            BorderThickness = new Thickness(1, 1, 1, 3),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 8,
            ContentMarginBottomOverride = 8,
        };

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        AddChild(root);

        // Picture + name + price
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        root.AddChild(top);

        var icon = new MarketplaceItemIcon(72, theme);
        icon.SetItem(data.DisplayEntity, data.ProtoId, data.Stackable ? data.Count : null);
        top.AddChild(icon);

        var titleBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        };
        top.AddChild(titleBox);

        var name = new RichTextLabel { HorizontalExpand = true };
        name.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[bold]{FormattedMessage.EscapeText(data.Name)}[/bold]"));
        titleBox.AddChild(name);

        var price = new RichTextLabel();
        var priceKey = data.Stackable ? "marketplace-card-price-unit" : "marketplace-card-price-single";
        price.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(priceKey,
            ("price", BankSystemExtensions.ToSpesoString(data.UnitPrice)),
            ("priceColor", PriceColor),
            ("mutedColor", MutedColor))));
        titleBox.AddChild(price);

        var stock = new RichTextLabel();
        stock.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-card-stock",
            ("count", data.Count),
            ("mutedColor", MutedColor))));
        titleBox.AddChild(stock);

        root.AddChild(_theme.MakeDivider());

        // Seller details
        var info = new RichTextLabel { HorizontalExpand = true };
        var infoMessage = new FormattedMessage();
        infoMessage.AddMarkupPermissive(Loc.GetString("marketplace-card-seller",
            ("seller", FormattedMessage.EscapeText(data.SellerName)),
            ("mutedColor", MutedColor)));
        infoMessage.PushNewline();
        infoMessage.AddMarkupPermissive(Loc.GetString("marketplace-card-id",
            ("idName", FormattedMessage.EscapeText(data.SellerIdName)),
            ("mutedColor", MutedColor)));
        infoMessage.PushNewline();
        infoMessage.AddMarkupPermissive(Loc.GetString("marketplace-card-station",
            ("grid", FormattedMessage.EscapeText(data.GridName)),
            ("id", data.Id),
            ("mutedColor", MutedColor),
            ("accentColor", AccentColor)));
        info.SetMessage(infoMessage);
        root.AddChild(info);

        // Controls
        if (data.Stackable && !data.IsOwn && data.Count > 1 && !readOnly)
        {
            var amountRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
            };
            amountRow.AddChild(new Label
            {
                Text = Loc.GetString("marketplace-card-amount"),
                VerticalAlignment = VAlignment.Center,
                FontColorOverride = Color.FromHex(MutedColor),
            });
            var spin = CreateAmountSpinBox(data.Count, _amount, value =>
            {
                _amount = value;
                OnAmountChanged?.Invoke(_data.Id, value);
                UpdateTotal();
            });
            spin.HorizontalExpand = true;
            amountRow.AddChild(spin);
            root.AddChild(amountRow);
        }

        var bottom = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(0, 2, 0, 0),
        };
        root.AddChild(bottom);

        _totalLabel = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        bottom.AddChild(_totalLabel);

        if (!readOnly)
        {
            _actionButton = new Button { MinWidth = 96 };
            if (data.IsOwn)
            {
                _actionButton.Text = Loc.GetString("marketplace-card-withdraw");
                _actionButton.AddStyleClass(StyleNano.StyleClassButtonColorRed);
                _actionButton.OnPressed += _ => OnWithdraw?.Invoke(_data.Id);
            }
            else
            {
                _actionButton.Text = Loc.GetString("marketplace-card-buy");
                _actionButton.AddStyleClass(StyleNano.StyleClassButtonColorGreen);
                _actionButton.OnPressed += _ => OnBuy?.Invoke(_data.Id, _data.Stackable ? _amount : 1, _data.UnitPrice);
                if (!hasAccount)
                    _actionButton.Disabled = true;
            }

            bottom.AddChild(_actionButton);
        }

        UpdateTotal();
    }

    private void UpdateTotal()
    {
        if (_readOnly && !_data.IsOwn)
        {
            // Without a quantity picker, show what the whole listing costs.
            var whole = (long) _data.UnitPrice * Math.Max(1, _data.Count);
            _totalLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-card-total-all",
                ("total", BankSystemExtensions.ToSpesoString((int) Math.Min(whole, int.MaxValue))),
                ("mutedColor", MutedColor),
                ("priceColor", PriceColor))));
            return;
        }

        if (_data.IsOwn)
        {
            _totalLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-card-own",
                ("accentColor", OwnBorder.ToHex()))));
            return;
        }

        var total = (long) _data.UnitPrice * (_data.Stackable ? _amount : 1);
        var affordable = total <= _balance;
        _totalLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-card-total",
            ("total", BankSystemExtensions.ToSpesoString((int) Math.Min(total, int.MaxValue))),
            ("color", affordable ? PriceColor : _theme.Danger),
            ("mutedColor", MutedColor))));
    }

    /// <summary>
    /// A spin box that clamps its value to 1..max instead of refusing out-of-range input.
    /// </summary>
    public static SpinBox CreateAmountSpinBox(int max, int initial, Action<int> onChanged)
    {
        var spin = new SpinBox { MinWidth = 170 };
        spin.AddLeftButton(-10, "-10");
        spin.AddLeftButton(-1, "-");
        spin.AddRightButton(1, "+");
        spin.AddRightButton(10, "+10");

        spin.OverrideValue(Math.Clamp(initial, 1, Math.Max(1, max)));
        spin.ValueChanged += args =>
        {
            var clamped = Math.Clamp(args.Value, 1, Math.Max(1, max));
            if (clamped != args.Value)
                spin.OverrideValue(clamped);

            onChanged(clamped);
        };

        return spin;
    }
}
