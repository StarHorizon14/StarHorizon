using Content.Shared._Horizon.GravityGun;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Horizon.GravityGun;

public sealed class GridDragGunVisualsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GridDraggedComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<GridDraggedComponent, ComponentShutdown>(OnShutdown);
        _overlay.AddOverlay(new GridDragGunOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<GridDragGunOverlay>();
    }

    private void OnState(Entity<GridDraggedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<GridDragGunComponent>(ent.Comp.Gun, out var gun))
            return;

        Paint(ent.Owner, gun.LineColor);
    }

    private void OnShutdown(Entity<GridDraggedComponent> ent, ref ComponentShutdown args)
    {
        Paint(ent.Owner, Color.White);
    }

    private void Paint(EntityUid grid, Color color)
    {
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (TryComp<SpriteComponent>(child, out var sprite))
                _sprite.SetColor((child, sprite), color);
        }
    }
}
