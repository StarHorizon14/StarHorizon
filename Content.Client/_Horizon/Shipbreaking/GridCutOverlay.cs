using System.Numerics;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client._Horizon.Shipbreaking;

/// <summary>
/// Draws the cuts of <see cref="GridCutsComponent"/> as thin gaps along the seams between tiles.
/// </summary>
public sealed class GridCutOverlay : Overlay
{
    // Same as the background seen through a hole in the hull.
    private static readonly Color GapColor = Color.Black;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    private readonly IEntityManager _entManager;
    private readonly SharedTransformSystem _transform;

    public GridCutOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        // After the walls and their tops, so the gaps show on walls.
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
                // Every cut is stored on both tiles, so only draw the north and east ones.
                if (edges.HasFlag(CutEdges.North))
                    handle.DrawRect(CutEdges.North.GetRect(tile, size, half), GapColor);

                if (edges.HasFlag(CutEdges.East))
                    handle.DrawRect(CutEdges.East.GetRect(tile, size, half), GapColor);
            }
        }

        // Cut edges of walls are drawn in the frame of the wall, so they stay on it when it is loose.
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

                // The rectangle of the edge for a tile whose lower left corner is at -0.5, -0.5.
                var rect = edge.GetRect(centre, size, half);
                handle.DrawRect(rect.Translated(new Vector2(-0.5f, -0.5f)), GapColor);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }
}
