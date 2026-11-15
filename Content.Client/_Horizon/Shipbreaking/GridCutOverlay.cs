using System.Numerics;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._Horizon.Shipbreaking;

public sealed class GridCutOverlay : Overlay
{
    private static readonly Color GapColor = Color.Black;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;

    public GridCutOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.WallTops + 1;
        _transform = entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var query = _entManager.EntityQueryEnumerator<GridCutsComponent, MapGridComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var cuts, out var grid, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            handle.SetTransform(_transform.GetWorldMatrix(uid));

            var size = grid.TileSize;
            var half = GridCutsComponent.CutWidthPixels / 32f / 2f * size;

            foreach (var (tile, edges) in cuts.Cuts)
            {
                // A cut only shows between two floor tiles, whatever is stored for tiles that are gone.
                if (!HasFloor(uid, grid, tile))
                    continue;

                if (edges.HasFlag(CutEdges.North) && HasFloor(uid, grid, tile + CutEdges.North.Offset()))
                    handle.DrawRect(CutEdges.North.GetRect(tile, size, half), GapColor);

                if (edges.HasFlag(CutEdges.East) && HasFloor(uid, grid, tile + CutEdges.East.Offset()))
                    handle.DrawRect(CutEdges.East.GetRect(tile, size, half), GapColor);
            }
        }

        var walls = _entManager.EntityQueryEnumerator<WallCutsComponent, TransformComponent>();
        while (walls.MoveNext(out var wall, out var wallCuts, out var wallXform))
        {
            if (wallXform.MapID != args.MapId)
                continue;

            handle.SetTransform(_transform.GetWorldMatrix(wall));

            const float size = 1f;
            var half = GridCutsComponent.CutWidthPixels / 32f / 2f;
            var centre = new Vector2i(0, 0);

            foreach (var edge in CutEdgesExtensions.All)
            {
                if (!wallCuts.Cuts.HasFlag(edge))
                    continue;

                var rect = edge.GetRect(centre, size, half);
                handle.DrawRect(rect.Translated(new Vector2(-0.5f, -0.5f)), GapColor);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private bool HasFloor(EntityUid uid, MapGridComponent grid, Vector2i tile)
    {
        return _entManager.System<SharedMapSystem>().TryGetTile(grid, tile, out var found) && !found.IsEmpty;
    }
}
