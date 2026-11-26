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
/// Framed picture of a real item. Hovering shows its examine text; right click or the examine key opens the full
/// examine popup with verbs.
/// Falls back to the prototype's icon until the entity itself reaches the client.
/// </summary>
public sealed class MarketplaceItemIcon : PanelContainer
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
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
    /// Examine text label of the tooltip currently shown, so the server's answer can replace it.
    /// </summary>
    private RichTextLabel? _tooltipText;

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
        if (item == _netEntity && _tooltipText != null)
            _tooltipText.SetMessage(text);
    }

    /// <summary>
    /// Builds the hover tooltip in the same style as the regular examine popup.
    /// </summary>
    private Control? BuildTooltip()
    {
        _tooltipText = null;

        string name;
        FormattedMessage text;
        var canExamine = false;

        if (_entity is { } uid && _entMan.EntityExists(uid))
        {
            name = _entMan.GetComponent<MetaDataComponent>(uid).EntityName;
            canExamine = true;

            if (_netEntity is { } net && MarketplaceExamineHub.TryGetCached(net, out var cached))
            {
                text = cached;
            }
            else
            {
                // Show what the client knows right away; the server fills in the rest.
                text = _entMan.System<ExamineSystemShared>().GetExamineText(uid, _player.LocalEntity);
                if (_netEntity is { } request)
                    MarketplaceExamineHub.Request(request);
            }
        }
        else if (_protoId != null && _proto.TryIndex<EntityPrototype>(_protoId, out var proto))
        {
            name = proto.Name;
            text = FormattedMessage.FromUnformatted(proto.Description);
        }
        else
        {
            return null;
        }

        var panel = new PanelContainer { MaxWidth = 400 };
        panel.AddStyleClass(ExamineSystem.StyleClassEntityTooltip);
        panel.ModulateSelfOverride = Color.LightGray.WithAlpha(0.95f);

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6, 2, 6, 4),
            SeparationOverride = 2,
        };
        panel.AddChild(box);

        var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 5 };
        if (canExamine)
        {
            var sprite = new SpriteView { OverrideDirection = Direction.South, SetSize = new Vector2(32, 32) };
            sprite.SetEntity(_entity);
            header.AddChild(sprite);
        }

        var title = new RichTextLabel { VerticalAlignment = VAlignment.Center };
        title.SetMessage(FormattedMessage.FromMarkupPermissive($"[bold]{FormattedMessage.EscapeText(name)}[/bold]"));
        header.AddChild(title);
        box.AddChild(header);

        var body = new RichTextLabel { MaxWidth = 385 };
        body.SetMessage(text);
        box.AddChild(body);

        if (canExamine)
        {
            _tooltipText = body;
            var more = new RichTextLabel();
            more.SetMessage(FormattedMessage.FromMarkupPermissive(
                $"[color=#8391A5][font size=9]{Loc.GetString("marketplace-examine-more")}[/font][/color]"));
            box.AddChild(more);
        }

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
        if (args.Function != ContentKeyFunctions.ExamineEntity && args.Function != EngineKeyFunctions.UseSecondary)
            return;

        if (_entity is not { } uid || !_entMan.EntityExists(uid))
            return;

        _entMan.System<ExamineSystem>().DoExamine(uid);
        args.Handle();
    }
}
