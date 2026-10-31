using System.Numerics;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Horizon.GridWelding.Systems;

/// <summary>
/// Shared geometry helpers for the grid welding tool. The actual grid merge
/// (<see cref="Robust.Server.Physics.GridFixtureSystem.Merge"/>) is server-only, but the math for
/// deciding whether two selected tiles are close/aligned enough to weld, and what offset+rotation
/// that weld implies, doesn't depend on anything server-exclusive.
/// </summary>
public abstract class SharedGridWeldingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    /// <summary>
    /// Works out whether two tiles on two different grids are close and squarely aligned enough
    /// to be welded together, and if so, the offset+rotation <see cref="Robust.Server.Physics.GridFixtureSystem.Merge"/>
    /// needs to make <paramref name="tileB"/> land immediately adjacent to <paramref name="tileA"/>.
    /// </summary>
    /// <param name="edgeA">The edge of <paramref name="tileA"/> that is to be joined.</param>
    /// <param name="edgeB">The edge of <paramref name="tileB"/> that is to be joined. It has to face <paramref name="edgeA"/>.</param>
    /// <param name="proximityTolerance">Maximum allowed gap/overlap at the shared edge, in world meters.</param>
    /// <param name="rotationTolerance">Maximum allowed deviation from a clean 90-degree snap.</param>
    /// <param name="offset">Grid-A-local tile offset to pass into <c>Merge</c>.</param>
    /// <param name="rotation">Rotation (snapped to the nearest cardinal direction) to pass into <c>Merge</c>.</param>
    /// <param name="targetTileA">The specific tile on grid A that <paramref name="tileB"/> will end up touching.</param>
    /// <param name="errorKey">A locale key describing why the geometry was rejected, if it was.</param>
    public bool TryComputeWeldGeometry(
        EntityUid gridAUid,
        MapGridComponent gridA,
        TransformComponent xformA,
        Vector2i tileA,
        CutEdges edgeA,
        EntityUid gridBUid,
        MapGridComponent gridB,
        TransformComponent xformB,
        Vector2i tileB,
        CutEdges edgeB,
        float proximityTolerance,
        Angle rotationTolerance,
        out Vector2i offset,
        out Angle rotation,
        out Vector2i targetTileA,
        out string? errorKey)
    {
        offset = default;
        rotation = Angle.Zero;
        targetTileA = default;
        errorKey = null;

        if (xformA.MapID == MapId.Nullspace || xformA.MapID != xformB.MapID)
        {
            errorKey = "grid-welding-invalid-target";
            return false;
        }

        var (_, worldRotA) = _transform.GetWorldPositionRotation(xformA);
        var (_, worldRotB) = _transform.GetWorldPositionRotation(xformB);

        var rawDiff = worldRotB - worldRotA;
        var quarterTurns = Math.Round(rawDiff.Theta / (Math.PI / 2));
        var snappedAngle = new Angle(quarterTurns * (Math.PI / 2));
        var snapError = Angle.ShortestDistance(rawDiff, snappedAngle);

        if (Math.Abs(snapError.Theta) > rotationTolerance.Theta)
        {
            errorKey = "grid-welding-not-aligned";
            return false;
        }

        rotation = snappedAngle;

        // The edge of B that is picked has to face back at the edge of A once B is turned into A's frame.
        var direction = edgeA.Offset();
        var rotatedEdge = rotation.RotateVec(new Vector2(edgeB.Offset().X, edgeB.Offset().Y));
        var facing = new Vector2i((int) MathF.Round(rotatedEdge.X), (int) MathF.Round(rotatedEdge.Y));
        if (direction == Vector2i.Zero || facing != new Vector2i(-direction.X, -direction.Y))
        {
            errorKey = "grid-welding-not-facing";
            return false;
        }

        var tileCenterA = _map.GridTileToWorldPos(gridAUid, gridA, tileA);
        var tileCenterB = _map.GridTileToWorldPos(gridBUid, gridB, tileB);
        var delta = tileCenterB - tileCenterA;

        // Express the gap between the two tiles in grid A's own local orientation. B has to sit one tile
        // ahead of the picked edge of A, and in line with it.
        var localDelta = (-worldRotA).RotateVec(delta);
        var tileSize = gridA.TileSize;
        var axial = direction.X != 0 ? localDelta.X * direction.X : localDelta.Y * direction.Y;
        var lateral = direction.X != 0 ? localDelta.Y : localDelta.X;

        if (Math.Abs(axial - tileSize) > proximityTolerance)
        {
            errorKey = "grid-welding-too-far";
            return false;
        }

        if (Math.Abs(lateral) > proximityTolerance)
        {
            errorKey = "grid-welding-not-facing";
            return false;
        }

        targetTileA = tileA + direction;

        // Derive the exact integer offset Merge expects (grid A's local tile-index space), using
        // the same half-tile-center math Merge itself applies internally.
        var half = gridA.TileSizeHalfVector;
        var rotatedTileB = rotation.RotateVec(new Vector2(tileB.X, tileB.Y));
        var rotatedHalfB = rotation.RotateVec(half);
        var targetCenterA = new Vector2(targetTileA.X, targetTileA.Y) + half;
        var offsetVec = targetCenterA - rotatedTileB - rotatedHalfB;

        offset = new Vector2i((int) MathF.Round(offsetVec.X), (int) MathF.Round(offsetVec.Y));
        return true;
    }

    /// <summary>
    /// Applies the same transform <see cref="Robust.Server.Physics.GridFixtureSystem.Merge"/> uses
    /// internally to a tile index on grid B, giving the resulting tile index on grid A.
    /// </summary>
    public static Vector2i TransformTile(Vector2i tile, Vector2 tileSizeHalfVector, Vector2i offset, Angle rotation)
    {
        var rotated = rotation.RotateVec(new Vector2(tile.X, tile.Y) + tileSizeHalfVector);
        var result = rotated + new Vector2(offset.X, offset.Y);
        return new Vector2i((int) MathF.Floor(result.X), (int) MathF.Floor(result.Y));
    }
}
