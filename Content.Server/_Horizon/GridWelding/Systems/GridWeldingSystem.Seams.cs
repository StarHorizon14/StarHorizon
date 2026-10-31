using Content.Server._Horizon.Shipbreaking;
using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.GridWelding.Systems;

// A welded border is not sealed: it is recorded as cuts (see GridCutsComponent), which leak air,
// show up as gaps and have to be welded shut one by one.
public sealed partial class GridWeldingSystem
{
    private static readonly Vector2i[] CardinalDirections =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
    };

    /// <summary>
    /// After a successful merge, cuts every border between what used to be grid A and what used to be grid B,
    /// besides the single explicit weld point, and carries the cuts grid B already had over to grid A.
    /// </summary>
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

                // This is the one pair the player deliberately welded - it's already sealed.
                if (transformed == weldedTransformedB && neighbor == tileA)
                    continue;

                _cuts.AddCut(gridAUid, transformed, CutEdgesExtensions.FromOffset(dir));
            }
        }

        if (originalBCuts == null)
            return;

        // Grid B is gone, so its own cuts move over: tile indices are transformed like the tiles were,
        // and the edges turn by the same number of quarter turns.
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
