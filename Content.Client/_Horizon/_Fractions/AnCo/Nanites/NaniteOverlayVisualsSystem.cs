using Content.Shared._Horizon._Fractions.AnCo.Nanites;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Horizon._Fractions.AnCo.Nanites;

/// <summary>
/// Добавляет/убирает спрайт-слой нанитов на клиенте при появлении/исчезновении
/// <see cref="NaniteOverlayComponent"/> на сущности.
/// </summary>
public sealed class NaniteOverlayVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath OverlayRsi = new("/Textures/Effects/emp.rsi");
    private const string OverlayState = "emp_pulse";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NaniteOverlayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NaniteOverlayComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<NaniteOverlayComponent> ent, ref ComponentInit args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), NaniteOverlayVisualLayers.Overlay, out _, false))
            return;

        _sprite.LayerMapReserve((ent, sprite), NaniteOverlayVisualLayers.Overlay);
        _sprite.LayerSetRsi((ent, sprite), NaniteOverlayVisualLayers.Overlay, OverlayRsi);
        _sprite.LayerSetRsiState((ent, sprite), NaniteOverlayVisualLayers.Overlay, OverlayState);
        sprite.LayerSetShader(NaniteOverlayVisualLayers.Overlay, "unshaded");
        _sprite.LayerSetVisible((ent, sprite), NaniteOverlayVisualLayers.Overlay, true);
    }

    private void OnShutdown(Entity<NaniteOverlayComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (_sprite.LayerMapTryGet((ent, sprite), NaniteOverlayVisualLayers.Overlay, out var layer, false))
            _sprite.RemoveLayer((ent, sprite), layer);
    }
}

public enum NaniteOverlayVisualLayers : byte
{
    Overlay
}
