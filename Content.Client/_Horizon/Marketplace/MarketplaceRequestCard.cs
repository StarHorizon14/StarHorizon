using Content.Client.Stylesheets;
using Content.Shared._Horizon.Marketplace;
using Content.Shared._NF.Bank;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// A request on the marketplace: what is wanted, how many, the reward, and either the offers made on it
/// (for the requester) or a way to offer the item in the console's tray (for everyone else).
/// </summary>
public sealed class MarketplaceRequestCard : PanelContainer
{
    private readonly MarketplaceTheme _theme;
    private Color CardBackground => _theme.CardBackground;
    private Color CardBorder => _theme.CardBorder;
    private Color OwnBorder => _theme.OwnBorder;
    private string MutedColor => _theme.Muted;
    private string PriceColor => _theme.Price;
    private string AccentColor => _theme.Accent;

    public event Action<int>? OnCancel;
    public event Action<int, int>? OnOffer;
    public event Action<int>? OnAccept;
    public event Action<int>? OnReject;
    public event Action<int, int>? OnOfferAmountChanged;

    /// <param name="slotName">Name of the item in the console tray, if any.</param>
    /// <param name="slotCount">Size of the stack in the tray, or 0 if it isn't stackable.</param>
    /// <param name="slotDeny">Why the tray item can't be offered, if it can't.</param>
    public MarketplaceRequestCard(MarketplaceTheme theme,
        MarketplaceRequestData data,
        string? slotName,
        int slotCount,
        string? slotDeny,
        int offerAmount,
        bool readOnly = false)
    {
        _theme = theme;
        HorizontalExpand = true;
        Margin = new Thickness(0, 0, 0, 8);

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = CardBackground,
            BorderColor = data.IsOwn ? OwnBorder : _theme.RequestBorder,
            BorderThickness = new Thickness(3, 1, 1, 1),
        };

        // Margin on the content rather than the style box, so wrapped text measures against the real width.
        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8, 10, 8),
        };
        AddChild(root);

        // Title + reward
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 10 };
        root.AddChild(top);

        var titleBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        top.AddChild(titleBox);

        var title = new RichTextLabel { HorizontalExpand = true };
        title.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[font size=13][bold]{FormattedMessage.EscapeText(data.Title)}[/bold][/font]"));
        titleBox.AddChild(title);

        var meta = new RichTextLabel();
        meta.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-request-card-meta",
            ("quantity", data.Quantity),
            ("offers", data.OfferCount),
            ("id", data.Id),
            ("mutedColor", MutedColor))));
        titleBox.AddChild(meta);

        var rewardBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalAlignment = VAlignment.Center,
        };
        top.AddChild(rewardBox);

        var reward = new RichTextLabel { HorizontalAlignment = HAlignment.Right };
        reward.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[font size=15][bold][color={PriceColor}]{BankSystemExtensions.ToSpesoString(data.Reward)}[/color][/bold][/font]"));
        rewardBox.AddChild(reward);

        var rewardLabel = new RichTextLabel { HorizontalAlignment = HAlignment.Right };
        rewardLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-request-card-reward",
            ("mutedColor", MutedColor))));
        rewardBox.AddChild(rewardLabel);

        // Description
        if (data.Description.Length > 0)
        {
            var description = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 2) };
            description.SetMessage(FormattedMessage.FromMarkupPermissive(
                $"[color=#C9D3E0]{FormattedMessage.EscapeText(data.Description)}[/color]"));
            root.AddChild(description);
        }

        var requester = new RichTextLabel { HorizontalExpand = true };
        requester.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString("marketplace-request-card-requester",
            ("name", FormattedMessage.EscapeText(data.RequesterName)),
            ("idName", FormattedMessage.EscapeText(data.RequesterIdName)),
            ("grid", FormattedMessage.EscapeText(data.GridName)),
            ("mutedColor", MutedColor),
            ("accentColor", AccentColor))));
        root.AddChild(requester);

        root.AddChild(_theme.MakeDivider());

        if (readOnly)
            BuildReadOnlyFooter(root, data);
        else if (data.IsOwn)
            BuildOwnerControls(root, data);
        else
            BuildOfferControls(root, data, slotName, slotCount, slotDeny, offerAmount);
    }

    /// <summary>
    /// PDA view: offers are made at a terminal. The requester can accept them here and have them teleported.
    /// </summary>
    private void BuildReadOnlyFooter(BoxContainer root, MarketplaceRequestData data)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(data.IsOwn
            ? Loc.GetString("marketplace-request-card-own-pda",
                ("count", data.OfferCount),
                ("accentColor", OwnBorder.ToHex()),
                ("mutedColor", MutedColor))
            : Loc.GetString("marketplace-request-card-offer-pda", ("mutedColor", MutedColor))));
        root.AddChild(label);

        if (!data.IsOwn)
            return;

        foreach (var offer in data.Offers)
        {
            var row = CreateOfferRow(_theme, offer, showRequest: false);
            var accept = new Button
            {
                Text = Loc.GetString("marketplace-delivery-accept"),
                VerticalAlignment = VAlignment.Center,
            };
            accept.AddStyleClass(StyleNano.StyleClassButtonColorGreen);
            accept.OnPressed += _ => OnAccept?.Invoke(offer.Id);
            row.Buttons.AddChild(accept);
            root.AddChild(row);
        }
    }

    private void BuildOwnerControls(BoxContainer root, MarketplaceRequestData data)
    {
        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        root.AddChild(header);

        var offersLabel = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        offersLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(
            data.Offers.Count == 0 ? "marketplace-request-card-no-offers" : "marketplace-request-card-offers-header",
            ("count", data.Offers.Count),
            ("mutedColor", MutedColor),
            ("accentColor", OwnBorder.ToHex()))));
        header.AddChild(offersLabel);

        var cancel = new Button { Text = Loc.GetString("marketplace-request-cancel") };
        cancel.AddStyleClass(StyleNano.StyleClassButtonColorRed);
        cancel.OnPressed += _ => OnCancel?.Invoke(data.Id);
        header.AddChild(cancel);

        foreach (var offer in data.Offers)
        {
            var row = CreateOfferRow(_theme, offer, showRequest: false);

            var accept = new Button { Text = Loc.GetString("marketplace-offer-accept"), VerticalAlignment = VAlignment.Center };
            accept.AddStyleClass(StyleNano.StyleClassButtonColorGreen);
            accept.OnPressed += _ => OnAccept?.Invoke(offer.Id);
            row.Buttons.AddChild(accept);

            var reject = new Button { Text = Loc.GetString("marketplace-offer-reject"), VerticalAlignment = VAlignment.Center };
            reject.OnPressed += _ => OnReject?.Invoke(offer.Id);
            row.Buttons.AddChild(reject);

            root.AddChild(row);
        }
    }

    private void BuildOfferControls(BoxContainer root,
        MarketplaceRequestData data,
        string? slotName,
        int slotCount,
        string? slotDeny,
        int offerAmount)
    {
        var bottom = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        root.AddChild(bottom);

        var status = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        bottom.AddChild(status);

        var amount = Math.Clamp(offerAmount > 0 ? offerAmount : Math.Min(slotCount, data.Quantity), 1, Math.Max(1, slotCount));
        if (slotName != null && slotDeny == null && slotCount > 1)
        {
            var spin = MarketplaceListingCard.CreateAmountSpinBox(slotCount, amount, value =>
            {
                amount = value;
                OnOfferAmountChanged?.Invoke(data.Id, value);
            });
            spin.MinWidth = 150;
            bottom.AddChild(spin);
        }

        var offerButton = new Button { MinWidth = 120 };
        offerButton.AddStyleClass(StyleNano.StyleClassButtonColorGreen);
        offerButton.Text = Loc.GetString("marketplace-offer-button");
        offerButton.OnPressed += _ => OnOffer?.Invoke(data.Id, amount);
        bottom.AddChild(offerButton);

        string statusText;
        if (slotName == null)
        {
            offerButton.Disabled = true;
            statusText = Loc.GetString("marketplace-offer-hint-empty", ("mutedColor", MutedColor));
        }
        else if (slotDeny != null)
        {
            offerButton.Disabled = true;
            statusText = $"[color={_theme.Danger}]{FormattedMessage.EscapeText(slotDeny)}[/color]";
        }
        else
        {
            statusText = Loc.GetString("marketplace-offer-hint-item",
                ("item", FormattedMessage.EscapeText(slotName)),
                ("mutedColor", MutedColor),
                ("accentColor", AccentColor));
        }

        if (data.HasOwnOffer)
            statusText += "\n" + Loc.GetString("marketplace-offer-hint-already", ("accentColor", OwnBorder.ToHex()));

        status.SetMessage(FormattedMessage.FromMarkupPermissive(statusText));
    }

    /// <summary>
    /// A compact row describing an offered item, with room for buttons on the right.
    /// </summary>
    public static OfferRow CreateOfferRow(MarketplaceTheme theme, MarketplaceOfferData offer, bool showRequest)
    {
        return new OfferRow(theme, offer, showRequest);
    }

    public sealed class OfferRow : PanelContainer
    {
        public readonly BoxContainer Buttons;

        public OfferRow(MarketplaceTheme theme, MarketplaceOfferData offer, bool showRequest)
        {
            var MutedColor = theme.Muted;
            var PriceColor = theme.Price;
            var AccentColor = theme.Accent;
            HorizontalExpand = true;
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = theme.RowBackground,
                BorderColor = theme.CardBorder,
                BorderThickness = new Thickness(1),
            };

            var box = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(6, 4, 6, 4),
            };
            AddChild(box);

            var icon = new MarketplaceItemIcon(48, theme);
            icon.SetItem(offer.DisplayEntity, offer.ProtoId, offer.Stackable ? offer.Count : null);
            box.AddChild(icon);

            var column = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                SeparationOverride = 2,
            };
            box.AddChild(column);

            // Name and buttons on the first line, details wrap underneath.
            var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            column.AddChild(header);

            var name = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            var count = offer.Stackable ? $" ×{offer.Count}" : string.Empty;
            name.SetMessage(FormattedMessage.FromMarkupPermissive(
                $"[bold]{FormattedMessage.EscapeText(offer.Name)}[/bold][color={PriceColor}]{count}[/color]"));
            header.AddChild(name);

            Buttons = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 4,
                VerticalAlignment = VAlignment.Center,
            };
            header.AddChild(Buttons);

            var details = new RichTextLabel { HorizontalExpand = true };
            var message = new FormattedMessage();
            if (showRequest)
            {
                message.AddMarkupPermissive(Loc.GetString("marketplace-offer-row-request",
                    ("title", FormattedMessage.EscapeText(offer.RequestTitle)),
                    ("reward", BankSystemExtensions.ToSpesoString(offer.RequestReward)),
                    ("mutedColor", MutedColor),
                    ("priceColor", PriceColor)));
                message.PushNewline();
                message.AddMarkupPermissive(Loc.GetString(offer.Status == MarketplaceOfferStatus.Pending
                        ? "marketplace-offer-status-pending"
                        : "marketplace-offer-status-returned",
                    ("mutedColor", MutedColor)));
            }
            else
            {
                message.AddMarkupPermissive(Loc.GetString("marketplace-offer-row-offerer",
                    ("name", FormattedMessage.EscapeText(offer.OffererName)),
                    ("idName", FormattedMessage.EscapeText(offer.OffererIdName)),
                    ("grid", FormattedMessage.EscapeText(offer.GridName)),
                    ("mutedColor", MutedColor),
                    ("accentColor", AccentColor)));
            }

            details.SetMessage(message);
            column.AddChild(details);
        }
    }
}
