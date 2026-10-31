using System.Numerics;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Client.Graphics;
using Robust.Shared.Map.Components;

namespace Content.Client.Shuttles.UI;

public partial class BaseShuttleControl
{
    private static readonly Color GridCutColor = Color.OrangeRed;

    private static readonly CutEdges[] CutEdgesDrawn = { CutEdges.North, CutEdges.East };

    private Vector2[] _cutVertices = new Vector2[64];

    private void DrawGridCuts(DrawingHandleScreen handle, Matrix3x2 gridToView, Entity<MapGridComponent> grid)
    {
        if (!EntManager.TryGetComponent(grid.Owner, out GridCutsComponent? cuts) || cuts.Cuts.Count == 0)
            return;

        var size = grid.Comp.TileSize;
        var count = 0;

        foreach (var (tile, edges) in cuts.Cuts)
        {
            foreach (var edge in CutEdgesDrawn)
            {
                if (!edges.HasFlag(edge))
                    continue;

                var rect = edge.GetRect(tile, size, 0f);
                if (count + 2 > _cutVertices.Length)
                    Array.Resize(ref _cutVertices, _cutVertices.Length * 2);

                _cutVertices[count++] = Vector2.Transform(rect.BottomLeft, gridToView);
                _cutVertices[count++] = Vector2.Transform(rect.TopRight, gridToView);
            }
        }

        if (count > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.LineList, new Span<Vector2>(_cutVertices, 0, count), GridCutColor);
    }
}
