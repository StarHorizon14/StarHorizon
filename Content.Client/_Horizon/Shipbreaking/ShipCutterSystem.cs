using Content.Client.Items;
using Content.Client.Items.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.PowerCell;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Horizon.Shipbreaking;

/// <summary>
/// Client side of the ship tool: shows a ghost of the target under the cursor while the tool is held,
/// and makes the mode visible: the sprite of the item and the line under the hand follow it.
/// </summary>
public sealed class ShipCutterSystem : SharedShipCutterSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ShipCutterGhostOverlay(EntityManager));

        SubscribeLocalEvent<ShipCutterComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<ShipCutterComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<ShipCutterComponent, GetInhandVisualsEvent>(OnGetHeldVisuals, after: [typeof(ItemSystem)]);
        Subs.ItemStatus<ShipCutterComponent>(ent => new ShipToolStatusControl(ent));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ShipCutterGhostOverlay>();
    }

    // Also runs for the first state, so a tool picked up in either mode looks right.
    private void OnState(Entity<ShipCutterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var cut = ent.Comp.Mode == ShipToolMode.Cut;
        var look = cut ? ent.Comp.CutSprite : ent.Comp.WeldSprite;
        if (look != null)
            _sprite.LayerSetSprite((ent, sprite), 0, look);

        _item.VisualsChanged(ent);

        // The flame under the tool shows the mode.
        if (_sprite.LayerMapTryGet((ent, sprite), "flame", out var flame, false))
            _sprite.LayerSetRsiState((ent, sprite), flame, cut ? "cutter_flame" : "welder_flame");
    }

    private void OnAppearance(Entity<ShipCutterComponent> ent, ref AppearanceChangeEvent args)
    {
        // The flame in the hands comes and goes with the charge.
        _item.VisualsChanged(ent);
    }

    /// <summary>
    /// While the tool has charge, the flame in the hand shows the mode: the flame of the cutter or of the welder.
    /// </summary>
    private void OnGetHeldVisuals(Entity<ShipCutterComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (ent.Comp.CutSprite is not SpriteSpecifier.Rsi rsi ||
            !_appearance.TryGetData<bool>(ent, PowerCellSlotVisuals.Enabled, out var powered) || !powered)
            return;

        var side = args.Location.ToString().ToLowerInvariant();
        var state = $"inhand-{side}-flame" + (ent.Comp.Mode == ShipToolMode.Cut ? "-cutter" : string.Empty);
        var key = $"inhand-{side}-flame";

        args.Layers.Add((key, new PrototypeLayerData
        {
            RsiPath = rsi.RsiPath.CanonPath,
            State = state,
            Shader = "unshaded",
            MapKeys = new HashSet<string> { key },
        }));
    }
}
