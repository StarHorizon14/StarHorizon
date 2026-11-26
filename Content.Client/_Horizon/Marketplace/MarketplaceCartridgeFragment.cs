using System.Linq;
using Content.Shared._Horizon.Marketplace;
using Content.Shared._NF.Bank;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// The PDA screen of a marketplace program. One column, no buying or offering - those happen at a terminal.
/// </summary>
public sealed class MarketplaceCartridgeFragment : BoxContainer
{
    public event Action<string, string, int, int>? OnCreateRequest;
    public event Action<int, NetEntity>? OnAcceptOffer;

    private readonly MarketplaceTheme _theme;
    private readonly RichTextLabel _header;
    private readonly LineEdit _listingSearch;
    private readonly BoxContainer _listings;
    private readonly Label _listingsEmpty;
    private readonly LineEdit _requestSearch;
    private readonly BoxContainer _requests;
    private readonly Label _requestsEmpty;
    private readonly MarketplaceRequestForm _form;
    private readonly TabContainer _tabs;
    private readonly BoxContainer _picker;

    /// <summary>
    /// Offer being delivered while the teleporter picker is open.
    /// </summary>
    private MarketplaceOfferData? _pickingFor;

    private MarketplaceCartridgeState? _state;

    public MarketplaceCartridgeFragment(MarketplaceTheme theme)
    {
        _theme = theme;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 6;
        Margin = new Thickness(5);

        var headerPanel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = theme.HeaderBackground,
                BorderColor = theme.HeaderBorder,
                BorderThickness = new Thickness(0, 0, 0, 2),
            },
        };
        _header = new RichTextLabel { Margin = new Thickness(8, 4, 8, 4) };
        headerPanel.AddChild(_header);
        AddChild(headerPanel);

        var tabs = new TabContainer { HorizontalExpand = true, VerticalExpand = true };
        _tabs = tabs;
        AddChild(tabs);

        // Shown instead of the tabs while choosing where a parcel goes.
        _picker = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
            Visible = false,
        };
        AddChild(_picker);

        (_listingSearch, _listings, _listingsEmpty) = AddListTab(tabs, "marketplace-search-placeholder", "marketplace-empty");
        _listingSearch.OnTextChanged += _ => RebuildListings();

        (_requestSearch, _requests, _requestsEmpty) = AddListTab(tabs, "marketplace-request-search-placeholder", "marketplace-request-empty");
        _requestSearch.OnTextChanged += _ => RebuildRequests();

        var formScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        var formPanel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = theme.CardBackground,
                BorderColor = theme.CardBorder,
                BorderThickness = new Thickness(1),
            },
        };
        _form = new MarketplaceRequestForm(theme, "marketplace-pda-request-note", descriptionHeight: 70)
        {
            Margin = new Thickness(10, 8, 10, 8),
        };
        _form.OnCreate += (title, description, quantity, reward) => OnCreateRequest?.Invoke(title, description, quantity, reward);
        formPanel.AddChild(_form);
        formScroll.AddChild(formPanel);
        tabs.AddChild(formScroll);

        tabs.SetTabTitle(0, Loc.GetString("marketplace-tab-market"));
        tabs.SetTabTitle(1, Loc.GetString("marketplace-tab-requests"));
        tabs.SetTabTitle(2, Loc.GetString("marketplace-pda-tab-new-request"));

        UpdateHeader();
    }

    private (LineEdit, BoxContainer, Label) AddListTab(TabContainer tabs, string placeholderKey, string emptyKey)
    {
        var page = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(0, 4, 0, 0) };
        var search = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString(placeholderKey) };
        page.AddChild(search);

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        var content = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(0, 0, 6, 0) };
        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        var empty = new Label
        {
            Text = Loc.GetString(emptyKey),
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 30, 0, 30),
            StyleClasses = { "LabelSubText" },
            Visible = false,
        };
        content.AddChild(list);
        content.AddChild(empty);
        scroll.AddChild(content);
        page.AddChild(scroll);
        tabs.AddChild(page);
        return (search, list, empty);
    }

    public void UpdateState(MarketplaceCartridgeState state)
    {
        _state = state;
        UpdateHeader();
        RebuildListings();
        RebuildRequests();
        // Keep the picker in sync (crystal count, teleporters), or close it if the offer is gone.
        if (_pickingFor != null)
        {
            var offer = state.Requests.SelectMany(r => r.Offers).FirstOrDefault(o => o.Id == _pickingFor.Id);
            if (offer == null)
                ClosePicker();
            else
                OpenPicker(offer);
        }

        _form.SetLimits(state.TaxRate,
            state.Balance,
            state.HasAccount,
            state.MaxReward,
            state.Requests.Count(r => r.IsOwn) >= state.MaxRequestsPerPlayer);
    }

    private void UpdateHeader()
    {
        var message = new FormattedMessage();
        message.AddMarkupPermissive(Loc.GetString(_theme.HeaderTitleKey));
        message.PushNewline();
        message.AddMarkupPermissive(Loc.GetString("marketplace-pda-subtitle",
            ("tax", _state == null ? 0 : (int) MathF.Round(_state.TaxRate * 100f)),
            ("listings", _state?.Listings.Count ?? 0),
            ("requests", _state?.Requests.Count ?? 0),
            ("balance", BankSystemExtensions.ToSpesoString(_state?.Balance ?? 0)),
            ("mutedColor", _theme.Muted),
            ("priceColor", _theme.Price)));
        _header.SetMessage(message);
    }

    private static bool Matches(string search, params string[] fields)
    {
        return fields.Any(f => f.Contains(search, StringComparison.CurrentCultureIgnoreCase));
    }

    private void RebuildListings()
    {
        _listings.RemoveAllChildren();
        if (_state == null)
            return;

        var search = _listingSearch.Text.Trim();
        var any = false;
        foreach (var listing in _state.Listings
                     .Where(l => search.Length == 0 || Matches(search, l.Name, l.SellerName, l.GridName, $"#{l.Id}"))
                     .OrderByDescending(l => l.ListedAt)
                     .ThenByDescending(l => l.Id))
        {
            _listings.AddChild(new MarketplaceListingCard(_theme, listing, _state.Balance, _state.HasAccount, readOnly: true));
            any = true;
        }

        _listingsEmpty.Visible = !any;
        _listingsEmpty.Text = Loc.GetString(_state.Listings.Count == 0 ? "marketplace-empty" : "marketplace-empty-search");
    }

    private void RebuildRequests()
    {
        _requests.RemoveAllChildren();
        if (_state == null)
            return;

        var search = _requestSearch.Text.Trim();
        var any = false;
        foreach (var request in _state.Requests
                     .Where(r => search.Length == 0 || Matches(search, r.Title, r.Description, r.RequesterName, r.GridName, $"#{r.Id}"))
                     .OrderByDescending(r => r.IsOwn)
                     .ThenByDescending(r => r.CreatedAt)
                     .ThenByDescending(r => r.Id))
        {
            var card = new MarketplaceRequestCard(_theme, request, null, 0, null, 0, readOnly: true);
            card.OnAccept += offerId =>
            {
                var offer = request.Offers.FirstOrDefault(o => o.Id == offerId);
                if (offer != null)
                    OpenPicker(offer);
            };
            _requests.AddChild(card);
            any = true;
        }

        _requestsEmpty.Visible = !any;
        _requestsEmpty.Text = Loc.GetString(_state.Requests.Count == 0 ? "marketplace-request-empty" : "marketplace-empty-search");
    }

    #region Teleport delivery

    private void OpenPicker(MarketplaceOfferData offer)
    {
        _pickingFor = offer;
        _tabs.Visible = false;
        _picker.Visible = true;
        _picker.RemoveAllChildren();

        var crystals = _state?.Crystals ?? 0;
        var cost = _state?.DeliveryCost ?? 1;
        var enoughCrystals = crystals >= cost;

        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = _theme.CardBackground,
                BorderColor = _theme.CardBorder,
                BorderThickness = new Thickness(1),
            },
        };
        _picker.AddChild(panel);

        var box = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(10, 8, 10, 8),
        };
        panel.AddChild(box);

        var header = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 8 };
        var icon = new MarketplaceItemIcon(48, _theme);
        icon.SetItem(null, offer.ProtoId, offer.Stackable ? offer.Count : null);
        header.AddChild(icon);

        var title = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        var titleMessage = new FormattedMessage();
        titleMessage.AddMarkupPermissive(Loc.GetString("marketplace-delivery-title"));
        titleMessage.PushNewline();
        titleMessage.AddMarkupPermissive(Loc.GetString("marketplace-delivery-item",
            ("item", FormattedMessage.EscapeText(offer.Name)),
            ("count", offer.Stackable ? offer.Count : 1),
            ("mutedColor", _theme.Muted)));
        title.SetMessage(titleMessage);
        header.AddChild(title);
        box.AddChild(header);

        var crystalLabel = new RichTextLabel();
        crystalLabel.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(
            enoughCrystals ? "marketplace-delivery-crystals" : "marketplace-delivery-crystals-missing",
            ("count", crystals),
            ("cost", cost),
            ("mutedColor", _theme.Muted),
            ("accentColor", _theme.Accent))));
        box.AddChild(crystalLabel);

        box.AddChild(_theme.MakeDivider());

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(0, 0, 6, 0) };
        scroll.AddChild(list);
        box.AddChild(scroll);

        var points = _state?.DeliveryPoints ?? new List<MarketplaceDeliveryPoint>();
        if (points.Count == 0)
        {
            list.AddChild(new Label
            {
                Text = Loc.GetString("marketplace-delivery-no-teleporters"),
                StyleClasses = { "LabelSubText" },
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20),
            });
        }

        foreach (var point in points)
        {
            var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, SeparationOverride = 6 };
            var name = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            name.SetMessage(FormattedMessage.FromMarkupPermissive(point.Powered
                ? $"[color={_theme.Accent}]•[/color] {FormattedMessage.EscapeText(point.Name)}"
                : $"[color={_theme.Danger}]•[/color] [color={_theme.Muted}]{FormattedMessage.EscapeText(point.Name)} · {Loc.GetString("marketplace-delivery-unpowered")}[/color]"));
            row.AddChild(name);

            var send = new Button
            {
                Text = Loc.GetString("marketplace-delivery-send"),
                Disabled = !point.Powered || !enoughCrystals,
                MinWidth = 110,
            };
            send.AddStyleClass(Content.Client.Stylesheets.StyleNano.StyleClassButtonColorGreen);
            var target = point.Teleporter;
            send.OnPressed += _ =>
            {
                OnAcceptOffer?.Invoke(offer.Id, target);
                ClosePicker();
            };
            row.AddChild(send);
            list.AddChild(row);
        }

        var cancel = new Button { Text = Loc.GetString("marketplace-delivery-cancel"), HorizontalExpand = true };
        cancel.OnPressed += _ => ClosePicker();
        box.AddChild(cancel);
    }

    private void ClosePicker()
    {
        _pickingFor = null;
        _picker.Visible = false;
        _picker.RemoveAllChildren();
        _tabs.Visible = true;
    }

    #endregion
}
