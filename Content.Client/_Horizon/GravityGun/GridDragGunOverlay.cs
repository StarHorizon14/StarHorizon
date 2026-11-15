using Content.Shared._Horizon.GravityGun;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._Horizon.GravityGun;

public sealed class GridDragGunOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;
    private readonly SharedMapSystem _maps;

    public GridDragGunOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        _transform = entManager.System<SharedTransformSystem>();
        _maps = entManager.System<SharedMapSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entManager.EntityQueryEnumerator<GridDraggedComponent, MapGridComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var dragged, out var grid, out var xform))
        {
            if (!_entManager.TryGetComponent(dragged.Gun, out GridDragGunComponent? gun) ||
                !_entManager.TryGetComponent(dragged.Gun, out TransformComponent? gunXform) ||
                xform.MapID != gunXform.MapID || xform.MapID != args.MapId)
                continue;

            var color = gun.LineColor;

            handle.SetTransform(_transform.GetWorldMatrix(uid));
            foreach (var tile in _maps.GetAllTiles(uid, grid))
            {
                handle.DrawRect(Box2.FromDimensions(new System.Numerics.Vector2(tile.GridIndices.X, tile.GridIndices.Y) * grid.TileSize,
                    new System.Numerics.Vector2(grid.TileSize)), color.WithAlpha(0.25f));
            }

            handle.SetTransform(System.Numerics.Matrix3x2.Identity);

            var gunPos = _transform.GetWorldPosition(gunXform);
            var gridPos = System.Numerics.Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(uid));
            var diff = gridPos - gunPos;
            var length = diff.Length() / 2f;
            var midPoint = gunPos + diff / 2;
            const float width = 0.05f;

            var box = new Box2(-width, -length, width, length);
            var rotated = new Box2Rotated(box.Translated(midPoint), diff.ToWorldAngle(), midPoint);
            handle.DrawRect(rotated, color.WithAlpha(0.3f));
        }
    }
}
