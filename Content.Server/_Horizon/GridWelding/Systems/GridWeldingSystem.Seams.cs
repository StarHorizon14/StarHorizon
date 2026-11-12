using Content.Server._Horizon.Shipbreaking;
using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.GridWelding.Systems;

public sealed partial class GridWeldingSystem
{
    private static readonly Vector2i[] CardinalDirections =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    private void ApplyMergeSeams(
        EntityUid gridAUid,
        MapGridComponent gridA,
        HashSet<Vector2i> originalAFootprint,
        List<TileRef> originalBTiles,
        Dictionary<Vector2i, CutEdges>? originalBCuts,
        Vector2i offset,
        Angle rotation,
        Vector2i tileA,
        Vector2i tileB)
    {
        var half = gridA.TileSizeHalfVector;
        var weldedTransformedB = SharedGridWeldingSystem.TransformTile(tileB, half, offset, rotation);

        foreach (var bTile in originalBTiles)
        {
            var transformed = SharedGridWeldingSystem.TransformTile(bTile.GridIndices, half, offset, rotation);

            foreach (var dir in CardinalDirections)
            {
                var neighbor = transformed + dir;
                if (!originalAFootprint.Contains(neighbor))
                    continue;

                if (transformed == weldedTransformedB && neighbor == tileA)
                    continue;

                _cuts.AddCut(gridAUid, transformed, CutEdgesExtensions.FromOffset(dir));
            }
        }

        if (originalBCuts == null)
            return;

        var quarterTurns = (int) Math.Round(rotation.Theta / (Math.PI / 2));
        foreach (var (tile, edges) in originalBCuts)
        {
            var transformed = SharedGridWeldingSystem.TransformTile(tile, half, offset, rotation);
            var rotated = edges.Rotate(quarterTurns);

            foreach (var edge in CutEdgesExtensions.All)
            {
                if (rotated.HasFlag(edge))
                    _cuts.AddCut(gridAUid, transformed, edge);
            }
        }
    }
}
