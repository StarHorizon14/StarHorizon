using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared._Horizon.Marketplace;
using Content.Shared._NF.Bank;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// Form for posting a request: title, description, quantity and reward. Used by the terminal and the PDA program.
/// </summary>
public sealed class MarketplaceRequestForm : BoxContainer
{
    public event Action<string, string, int, int>? OnCreate;

    private readonly MarketplaceTheme _theme;
    private readonly LineEdit _title;
    private readonly TextEdit _description;
    private readonly LineEdit _reward;
    private readonly RichTextLabel _summary;
    private readonly Button _create;

    private int _quantity = 1;
    private float _taxRate;
    private int _balance;
    private bool _hasAccount;
    private int _maxReward = int.MaxValue;
    private bool _limitReached;

    public MarketplaceRequestForm(MarketplaceTheme theme, string? noteKey = null, float descriptionHeight = 90)
    {
        _theme = theme;
        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 6;
        HorizontalExpand = true;

        AddChild(new Label { Text = Loc.GetString("marketplace-request-create-title"), StyleClasses = { "LabelHeading" } });

        AddChild(MakeFieldLabel("marketplace-request-field-title"));
        _title = new LineEdit
        {
            PlaceHolder = Loc.GetString("marketplace-request-field-title-placeholder"),
            IsValid = text => text.Length <= MarketplaceConsoleComponent.MaxTitleLength,
        };
        _title.OnTextChanged += _ => UpdateSummary();
        AddChild(_title);

        AddChild(MakeFieldLabel("marketplace-request-field-description"));
        var descriptionFrame = new PanelContainer { PanelOverride = theme.MakeInset() };
        _description = new TextEdit { MinHeight = descriptionHeight, HorizontalExpand = true };
        descriptionFrame.AddChild(_description);
        AddChild(descriptionFrame);

        AddChild(MakeFieldLabel("marketplace-request-field-quantity"));
        var quantity = MarketplaceListingCard.CreateAmountSpinBox(MarketplaceConsoleComponent.MaxRequestQuantity, _quantity, value =>
        {
            _quantity = value;
            UpdateSummary();
        });
        quantity.HorizontalExpand = true;
        AddChild(quantity);

        AddChild(MakeFieldLabel("marketplace-request-field-reward"));
        _reward = new LineEdit
        {
            PlaceHolder = "0",
            IsValid = text => text.Length == 0 || text.Length <= 9 && text.All(char.IsDigit),
        };
        _reward.OnTextChanged += _ => UpdateSummary();
        AddChild(_reward);

        var summaryPanel = new PanelContainer { PanelOverride = theme.MakeInset() };
        _summary = new RichTextLabel { Margin = new Thickness(8, 6, 8, 6) };
        summaryPanel.AddChild(_summary);
        AddChild(summaryPanel);

        AddChild(new Control { VerticalExpand = true });

        _create = new Button
        {
            Text = Loc.GetString("marketplace-request-create-button"),
            MinHeight = 36,
            HorizontalExpand = true,
        };
        _create.AddStyleClass(StyleNano.StyleClassButtonColorGreen);
        _create.OnPressed += _ => Submit();
        AddChild(_create);

        if (noteKey != null)
        {
            var note = new RichTextLabel();
            note.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(noteKey, ("mutedColor", theme.Muted))));
            AddChild(note);
        }

        UpdateSummary();
    }

    private Label MakeFieldLabel(string key)
    {
        return new Label { Text = Loc.GetString(key), StyleClasses = { "LabelSubText" } };
    }

    /// <summary>
    /// Updates what the form needs to know about the player and the network.
    /// </summary>
    public void SetLimits(float taxRate, int balance, bool hasAccount, int maxReward, bool limitReached)
    {
        _taxRate = taxRate;
        _balance = balance;
        _hasAccount = hasAccount;
        _maxReward = maxReward;
        _limitReached = limitReached;
        UpdateSummary();
    }

    private void Submit()
    {
        if (!int.TryParse(_reward.Text, out var reward) || reward <= 0)
            return;

        var description = Rope.Collapse(_description.TextRope).Trim();
        if (description.Length > MarketplaceConsoleComponent.MaxDescriptionLength)
            description = description[..MarketplaceConsoleComponent.MaxDescriptionLength];

        OnCreate?.Invoke(_title.Text.Trim(), description, _quantity, reward);

        _title.Text = string.Empty;
        _description.TextRope = new Rope.Leaf(string.Empty);
        _reward.Text = string.Empty;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var hasReward = int.TryParse(_reward.Text, out var reward) && reward > 0;
        var tax = hasReward ? SharedMarketplaceSystem.GetTax(reward, _taxRate) : 0;

        var summary = new FormattedMessage();
        summary.AddMarkupPermissive(Loc.GetString("marketplace-request-summary-held",
            ("reward", BankSystemExtensions.ToSpesoString(hasReward ? reward : 0)),
            ("mutedColor", _theme.Muted)));
        summary.PushNewline();
        summary.AddMarkupPermissive(Loc.GetString("marketplace-request-summary-payout",
            ("payout", BankSystemExtensions.ToSpesoString(hasReward ? reward - tax : 0)),
            // What the supplier keeps after tax, e.g. 90%.
            ("share", 100 - (int) MathF.Round(_taxRate * 100f)),
            ("mutedColor", _theme.Muted),
            ("priceColor", _theme.Price)));
        _summary.SetMessage(summary);

        _create.Disabled = !_hasAccount
                           || _title.Text.Trim().Length == 0
                           || !hasReward
                           || reward > _maxReward
                           || reward > _balance
                           || _limitReached;
    }
}
