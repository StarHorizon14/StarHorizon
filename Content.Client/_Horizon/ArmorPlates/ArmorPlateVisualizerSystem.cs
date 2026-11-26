using Content.Shared._Horizon.ArmorPlates;
using Robust.Client.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Client._Horizon.ArmorPlates;

/// <summary>
/// Переключает спрайт бронепластины: {prefix}-icon в инвентаре/руках,
/// {prefix}-world на полу и {prefix}-broken для сломанной пластины.
/// </summary>
public sealed class ArmorPlateVisualizerSystem : VisualizerSystem<ArmorPlateComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorPlateComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<ArmorPlateComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<ArmorPlateComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<ArmorPlateComponent> ent, ref ComponentStartup args)
    {
        UpdateSprite(ent);
    }

    private void OnParentChanged(Entity<ArmorPlateComponent> ent, ref EntParentChangedMessage args)
    {
        UpdateSprite(ent);
    }

    private void OnHandleState(Entity<ArmorPlateComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateSprite(ent);
    }

    protected override void OnAppearanceChange(EntityUid uid, ArmorPlateComponent component, ref AppearanceChangeEvent args)
    {
        UpdateSprite((uid, component), args.Sprite);
    }

    private void UpdateSprite(Entity<ArmorPlateComponent> ent, SpriteComponent? sprite = null)
    {
        if (ent.Comp.SpritePrefix is not { } prefix)
            return;

        if (sprite == null && !TryComp(ent, out sprite))
            return;

        var broken = ent.Comp.Broken;
        if (AppearanceSystem.TryGetData<bool>(ent, ArmorPlateVisuals.Broken, out var appearanceBroken))
            broken |= appearanceBroken;

        string state;
        if (broken)
            state = $"{prefix}-broken";
        else
        {
            var parent = Transform(ent).ParentUid;
            var onFloor = HasComp<MapGridComponent>(parent) || HasComp<MapComponent>(parent);
            state = onFloor ? $"{prefix}-world" : $"{prefix}-icon";
        }

        SpriteSystem.LayerSetRsiState((ent, sprite), 0, state);
    }
}
