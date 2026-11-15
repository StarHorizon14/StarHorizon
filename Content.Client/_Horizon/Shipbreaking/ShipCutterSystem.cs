using Content.Client.Items;
using Content.Client.Items.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.Hands;
using Content.Shared.Item;
using Content.Shared.PowerCell;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Utility;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Horizon.Shipbreaking;

public sealed class ShipCutterSystem : SharedShipCutterSystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new ShipCutterGhostOverlay(EntityManager));

        SubscribeLocalEvent<ShipCutterComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<ShipCutterComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<ShipCutterComponent, GetInhandVisualsEvent>(OnGetHeldVisuals, after: [typeof(ItemSystem)]);
        Subs.ItemStatus<ShipCutterComponent>(ent => new ShipToolStatusControl(ent));

        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EditorRotateObject, new PointerInputCmdHandler(HandleRotate, outsidePrediction: true))
            .Register<ShipCutterSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<ShipCutterGhostOverlay>();
        CommandBinds.Unregister<ShipCutterSystem>();
    }

    private bool HandleRotate(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            !_handsSystem.TryGetActiveItem(player, out var held) ||
            !TryComp<ShipCutterComponent>(held, out var cutter) ||
            cutter.Mode != ShipToolMode.Cut)
            return false;

        RaiseNetworkEvent(new ShipCutterRotateEdgeEvent());
        return true;
    }

    private void OnState(Entity<ShipCutterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var cut = ent.Comp.Mode == ShipToolMode.Cut;
        var look = cut ? ent.Comp.CutSprite : ent.Comp.WeldSprite;
        if (look != null)
            _sprite.LayerSetSprite((ent, sprite), 0, look);

        _item.VisualsChanged(ent);

        if (_sprite.LayerMapTryGet((ent, sprite), "flame", out var flame, false))
            _sprite.LayerSetRsiState((ent, sprite), flame, cut ? "cutter_flame" : "welder_flame");
    }

    private void OnAppearance(Entity<ShipCutterComponent> ent, ref AppearanceChangeEvent args)
    {
        _item.VisualsChanged(ent);
    }

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
