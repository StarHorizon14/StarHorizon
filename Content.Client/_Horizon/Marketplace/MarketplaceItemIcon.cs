using System.Numerics;
using Content.Client.Examine;
using Content.Shared.Examine;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._Horizon.Marketplace;

/// <summary>
/// Framed picture of a real item. Hovering shows a hint; Shift+click (the examine key) opens the full examine popup.
/// Falls back to the prototype's icon until the entity itself reaches the client.
/// </summary>
public sealed class MarketplaceItemIcon : PanelContainer
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly Robust.Client.Player.IPlayerManager _player = default!;

    private readonly Color _border;
    private readonly Color _borderHover;

    private readonly StyleBoxFlat _style;
    private readonly SpriteView _spriteView;
    private readonly EntityPrototypeView _protoView;
    private readonly Label _badge;

    private NetEntity? _netEntity;
    private bool _hasPrototype;
    private EntityUid? _entity;
    private string? _protoId;

    /// <summary>
    /// Set while waiting for the server's examine text after the examine key was pressed.
    /// </summary>
    private bool _examinePending;

    public MarketplaceItemIcon(float size, MarketplaceTheme? theme = null)
    {
        IoCManager.InjectDependencies(this);
        theme ??= MarketplaceTheme.Sector;
        _border = theme.IconBorder;
        _borderHover = theme.IconBorderHover;

        SetSize = new Vector2(size, size);
        MouseFilter = MouseFilterMode.Stop;

        _style = new StyleBoxFlat
        {
            BackgroundColor = theme.Inset,
            BorderColor = _border,
            BorderThickness = new Thickness(1),
        };
        PanelOverride = _style;

        // Item sprites are 32px; scale them up by a whole factor so pixel art stays crisp.
        var scale = Math.Max(1f, MathF.Floor((size - 8) / 32f));

        _spriteView = new SpriteView
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(size - 8, size - 8),
            Scale = new Vector2(scale, scale),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Visible = false,
        };
        _protoView = new EntityPrototypeView
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(size - 8, size - 8),
            Scale = new Vector2(scale, scale),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Visible = false,
        };
        _badge = new Label
        {
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0, 0, 4, 1),
            FontColorOverride = Color.FromHex("#FFD866"),
            Visible = false,
        };

        AddChild(_spriteView);
        AddChild(_protoView);
        AddChild(_badge);

        OnKeyBindDown += HandleKeyBind;

        TooltipDelay = 0.15f;
        TooltipSupplier = _ => BuildTooltip();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        MarketplaceExamineHub.Received += OnExamineReceived;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        MarketplaceExamineHub.Received -= OnExamineReceived;
    }

    private void OnExamineReceived(NetEntity item, FormattedMessage text)
    {
        if (!_examinePending || item != _netEntity)
            return;

        _examinePending = false;
        FillExamine(text);
    }

    /// <summary>
    /// Opens the popup right now, under the cursor. The text may arrive later from the server, and by then
    /// the cursor could have moved, so the position must not wait for it.
    /// </summary>
    private void OpenExamine()
    {
        if (_entity is not { } uid || !_entMan.EntityExists(uid) || _player.LocalEntity is not { } player)
            return;

        _entMan.System<ExamineSystem>().OpenTooltip(player, uid, centeredOnCursor: true, openAtOldTooltip: false);
    }

    private void FillExamine(FormattedMessage text)
    {
        if (_entity is not { } uid || !_entMan.EntityExists(uid) || _player.LocalEntity is not { } player)
            return;

        _entMan.System<ExamineSystem>().UpdateTooltipInfo(player, uid, text, getVerbs: false);
    }

    /// <summary>
    /// Hover tooltip: just a small plate telling how to examine the item.
    /// </summary>
    private Control? BuildTooltip()
    {
        if (_entity is not { } uid || !_entMan.EntityExists(uid))
            return null;

        var panel = new PanelContainer();
        panel.AddStyleClass(ExamineSystem.StyleClassEntityTooltip);
        panel.ModulateSelfOverride = Color.LightGray.WithAlpha(0.95f);

        var label = new RichTextLabel { Margin = new Thickness(6, 3, 6, 3) };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(
            $"[color=#8391A5]{FormattedMessage.EscapeText(Loc.GetString("marketplace-examine-hint"))}[/color]"));
        panel.AddChild(label);
        return panel;
    }

    public void SetItem(NetEntity? netEntity, string? protoId, int? count = null)
    {
        _netEntity = netEntity;
        _entity = null;

        _protoId = protoId;
        _hasPrototype = protoId != null;
        _protoView.SetPrototype(protoId == null ? (EntProtoId?) null : new EntProtoId(protoId));
        _protoView.Visible = _hasPrototype;
        _spriteView.Visible = false;

        _badge.Visible = count is > 1;
        _badge.Text = count is > 1 ? $"×{count}" : string.Empty;

        TryResolveEntity();
    }

    private void TryResolveEntity()
    {
        if (_entity != null && _entMan.EntityExists(_entity))
            return;

        _entity = null;
        if (_netEntity is not { } net || !_entMan.TryGetEntity(net, out var uid) || !_entMan.EntityExists(uid))
        {
            _spriteView.Visible = false;
            _protoView.Visible = _hasPrototype;
            return;
        }

        _entity = uid;
        _spriteView.SetEntity(uid);
        _spriteView.Visible = true;
        _protoView.Visible = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Listed items stream in through PVS shortly after the UI opens.
        if (_netEntity != null && (_entity == null || !_entMan.EntityExists(_entity)))
            TryResolveEntity();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _style.BorderColor = _borderHover;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _style.BorderColor = _border;
    }

    private void HandleKeyBind(GUIBoundKeyEventArgs args)
    {
        if (args.Function != ContentKeyFunctions.ExamineEntity)
            return;

        if (_netEntity is not { } net || _entity is not { } uid || !_entMan.EntityExists(uid))
            return;

        // Items sit in the market storage, so the full text (incl. armor resists) has to come from the server.
        HideTooltip();
        OpenExamine();
        if (MarketplaceExamineHub.TryGetCached(net, out var cached))
        {
            FillExamine(cached);
        }
        else
        {
            _examinePending = true;
            MarketplaceExamineHub.Request(net);
        }

        args.Handle();
    }
}
