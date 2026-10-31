using System.Numerics;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Horizon.GridWelding.Systems;

public abstract class SharedGridWeldingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

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

        var half = gridA.TileSizeHalfVector;
        var rotatedTileB = rotation.RotateVec(new Vector2(tileB.X, tileB.Y));
        var rotatedHalfB = rotation.RotateVec(half);
        var targetCenterA = new Vector2(targetTileA.X, targetTileA.Y) + half;
        var offsetVec = targetCenterA - rotatedTileB - rotatedHalfB;

        offset = new Vector2i((int) MathF.Round(offsetVec.X), (int) MathF.Round(offsetVec.Y));
        return true;
    }

    public static Vector2i TransformTile(Vector2i tile, Vector2 tileSizeHalfVector, Vector2i offset, Angle rotation)
    {
        var rotated = rotation.RotateVec(new Vector2(tile.X, tile.Y) + tileSizeHalfVector);
        var result = rotated + new Vector2(offset.X, offset.Y);
        return new Vector2i((int) MathF.Floor(result.X), (int) MathF.Floor(result.Y));
    }
}
