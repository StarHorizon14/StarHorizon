using System.Numerics;
using System.Linq;
using Content.Shared._Horizon.GridWelding.Components;
using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._Horizon.GridWelding.Systems;

public sealed partial class GridWeldingSystem
{
    /// <summary>
    /// Runs the full validation checklist for welding <paramref name="tileA"/> on grid A to
    /// <paramref name="tileB"/> on grid B, and if everything checks out, returns the offset+rotation
    /// <see cref="Robust.Server.Physics.GridFixtureSystem.Merge"/> needs. Always re-run this right
    /// before actually calling Merge, not just at the initial click, since the two grids can
    /// move/change during the DoAfter.
    /// </summary>
    private bool TryValidateWeld(
        SeamWelderComponent comp,
        EntityUid user,
        EntityUid gridAUid,
        Vector2i tileA,
        CutEdges edgeA,
        EntityUid gridBUid,
        Vector2i tileB,
        CutEdges edgeB,
        out Vector2i offset,
        out Angle rotation,
        out string? errorKey)
    {
        offset = default;
        rotation = Angle.Zero;
        errorKey = null;

        if (gridAUid == gridBUid)
        {
            errorKey = "grid-welding-same-grid";
            return false;
        }

        if (!TryComp<MapGridComponent>(gridAUid, out var gridA) ||
            !TryComp<MapGridComponent>(gridBUid, out var gridB) ||
            !TryComp<TransformComponent>(gridAUid, out var xformA) ||
            !TryComp<TransformComponent>(gridBUid, out var xformB))
        {
            errorKey = "grid-welding-invalid-target";
            return false;
        }

        if (HasComp<MapComponent>(gridAUid) || HasComp<MapComponent>(gridBUid))
        {
            errorKey = "grid-welding-invalid-target";
            return false;
        }

        if (!TryComputeWeldGeometry(gridAUid, gridA, xformA, tileA, edgeA, gridBUid, gridB, xformB, tileB, edgeB,
                comp.TileProximityTolerance, comp.RotationSnapTolerance,
                out offset, out rotation, out var targetTileA, out errorKey))
        {
            return false;
        }

        var tileCountA = _maps.GetAllTiles(gridAUid, gridA).Count();
        var tileCountB = _maps.GetAllTiles(gridBUid, gridB).Count();
        if (tileCountA + tileCountB > comp.MaxMergedGridTiles)
        {
            errorKey = "grid-welding-too-large";
            return false;
        }

        // Full-footprint overlap check: transform every tile of B into A's local frame. Nothing is allowed to land on
        // an occupied tile of A or on a hard structure of A. Structures of B are part of B and only matter where
        // they land on A, which is the same check.
        // If the grids fit almost but not quite (a snug notch), B may be nudged by a tile along the seam: the
        // straight position is tried first, then one tile either way.
        var half = gridA.TileSizeHalfVector;
        var tilesB = _maps.GetAllTiles(gridBUid, gridB).ToList();
        var direction = targetTileA - tileA;
        var along = new Vector2i(Math.Abs(direction.Y), Math.Abs(direction.X));

        var fits = false;
        var straightOffset = offset;
        foreach (var shift in new[] { 0, 1, -1 })
        {
            var candidate = straightOffset + along * shift;
            if (!FootprintFits(gridAUid, gridA, tilesB, half, candidate, rotation))
                continue;

            offset = candidate;
            fits = true;
            break;
        }

        if (!fits)
        {
            errorKey = "grid-welding-obstructed";
            return false;
        }

        // The tolerance above is grid-to-grid alignment, not player reach - the player still
        // needs to actually be able to touch both points, not just whichever one they clicked.
        var coordsA = _maps.GridTileToLocal(gridAUid, gridA, tileA);
        var coordsB = _maps.GridTileToLocal(gridBUid, gridB, tileB);
        if (!_interaction.InRangeUnobstructed(user, coordsA, popup: false) ||
            !_interaction.InRangeUnobstructed(user, coordsB, popup: false))
        {
            errorKey = "grid-welding-out-of-range";
            return false;
        }

        return true;
    }

    private bool FootprintFits(EntityUid gridAUid, MapGridComponent gridA, List<TileRef> tilesB, Vector2 half, Vector2i offset, Angle rotation)
    {
        foreach (var bTile in tilesB)
        {
            var transformed = SharedGridWeldingSystem.TransformTile(bTile.GridIndices, half, offset, rotation);

            if (_maps.TryGetTileRef(gridAUid, gridA, transformed, out var existing) && !existing.Tile.IsEmpty)
                return false;

            if (_maps.GetAnchoredEntities(gridAUid, gridA, transformed).Any(HasHardFixture))
                return false;
        }

        return true;
    }

    private bool HasHardFixture(EntityUid uid)
    {
        return TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.Fixtures.Values.Any(f => f.Hard);
    }
}
