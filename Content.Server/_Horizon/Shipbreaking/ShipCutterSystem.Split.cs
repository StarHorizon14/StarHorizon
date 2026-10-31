using System.Reflection;
using Content.Server.Shuttles.Components;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Horizon.Shipbreaking;

// Splitting a grid along cuts.
public sealed partial class ShipCutterSystem
{
    // The engine moves anchored entities between grids with SharedTransformSystem.ReAnchor, which keeps them anchored
    // and sends only a ReAnchorEvent. It is internal to the engine, so it is called by reflection.
    private static readonly MethodInfo? ReAnchorMethod = typeof(SharedTransformSystem).GetMethod(
        "ReAnchor",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// After a cut between <paramref name="a"/> and <paramref name="b"/>, checks whether the two tiles are still
    /// connected through tiles and uncut seams. If not, the smaller side is split off into a new grid.
    /// </summary>
    private void TrySplit(EntityUid gridUid, MapGridComponent grid, GridCutsComponent cuts, Vector2i a, Vector2i b)
    {
        // The seam may be on a part that was cut off earlier: nothing to split there.
        if (!MapSystem.TryGetTile(grid, a, out var tileA) || tileA.IsEmpty ||
            !MapSystem.TryGetTile(grid, b, out var tileB) || tileB.IsEmpty)
            return;

        var sideA = Flood(grid, cuts, a);
        if (sideA.Contains(b))
            return;

        var sideB = Flood(grid, cuts, b);
        SplitOff(gridUid, grid, cuts, sideA.Count <= sideB.Count ? sideA : sideB);
    }

    /// <summary>
    /// All tiles reachable from <paramref name="start"/> without crossing a cut or an empty tile.
    /// </summary>
    private HashSet<Vector2i> Flood(MapGridComponent grid, GridCutsComponent cuts, Vector2i start)
    {
        var result = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);

        while (queue.TryDequeue(out var current))
        {
            foreach (var edge in CutEdgesExtensions.All)
            {
                if (cuts.IsCut(current, edge))
                    continue;

                var next = current + edge.Offset();
                if (result.Contains(next) || !MapSystem.TryGetTile(grid, next, out var tile) || tile.IsEmpty)
                    continue;

                result.Add(next);
                queue.Enqueue(next);
            }
        }

        return result;
    }

    private void SplitOff(EntityUid oldUid, MapGridComponent oldGrid, GridCutsComponent oldCuts, HashSet<Vector2i> moved)
    {
        var newGrid = MapManager.CreateGridEntity(Transform(oldUid).MapID);
        var (position, rotation) = TransformSystem.GetWorldPositionRotation(oldUid);
        TransformSystem.SetWorldPositionRotation(newGrid.Owner, position, rotation);

        var newTiles = new List<(Vector2i, Tile)>(moved.Count);
        var emptyTiles = new List<(Vector2i, Tile)>(moved.Count);
        foreach (var indices in moved)
        {
            if (MapSystem.TryGetTile(oldGrid, indices, out var tile))
                newTiles.Add((indices, tile));

            emptyTiles.Add((indices, Tile.Empty));
        }

        MapSystem.SetTiles(newGrid.Owner, newGrid.Comp, newTiles);

        // Everything standing on the moved tiles goes with them. Both grids share the same transform,
        // so local positions stay the same.
        var oldGridXform = Transform(oldUid);
        var newGridXform = Transform(newGrid.Owner);
        // Anchored things are read from the tile itself (cables and pipes have no fixtures, so the lookup could miss them),
        // loose things from the lookup, and a loose thing belongs to the tile its centre is on.
        var anchoredEntities = new List<(EntityUid Uid, Vector2i Tile)>();
        var looseEntities = new HashSet<EntityUid>();
        foreach (var indices in moved)
        {
            foreach (var uid in MapSystem.GetAnchoredEntities(oldUid, oldGrid, indices))
            {
                anchoredEntities.Add((uid, indices));
            }

            _lookup.GetLocalEntitiesIntersecting(oldUid, indices, looseEntities, 0f,
                LookupFlags.All | ~LookupFlags.Uncontained | LookupFlags.Approximate);
        }

        foreach (var (uid, indices) in anchoredEntities)
        {
            var xform = Transform(uid);
            if (HasComp<MapGridComponent>(uid))
                continue;

            // Moved the way the engine does when it splits a grid: the entity stays anchored and only gets a
            // ReAnchorEvent. Unanchoring it would make things react as if it was torn off (a cable would drop
            // as a coil and delete itself, machines would switch off).
            ReAnchor(uid, xform, oldGrid, newGrid.Comp, indices, oldUid, newGrid.Owner, oldGridXform, newGridXform);
        }

        foreach (var uid in looseEntities)
        {
            var xform = Transform(uid);
            if (xform.Anchored || xform.ParentUid != oldUid || HasComp<MapGridComponent>(uid))
                continue;

            var indices = MapSystem.LocalToTile(oldUid, oldGrid, xform.Coordinates);
            if (!moved.Contains(indices))
                continue;

            TransformSystem.SetCoordinates(uid, xform, new EntityCoordinates(newGrid.Owner, xform.LocalPosition));
        }

        // Like the engine does when it splits a grid: tell content before the old tiles are cleared, so decals move
        // to the new grid and it joins the station of the old one.
        var postSplit = new PostGridSplitEvent(oldUid, newGrid.Owner);
        RaiseLocalEvent(oldUid, ref postSplit, true);

        MapSystem.SetTiles(oldUid, oldGrid, emptyTiles);

        // The piece takes its cuts along. This is the path of the cutter: the event above comes while the old tiles are
        // still there, so GridCutsSystem does not move anything for it. The engine splitting a grid on its own
        // (damage) is handled by GridCutsSystem from the event alone.
        _cuts.MoveCuts(oldUid, oldCuts, newGrid.Owner, moved);

        // The piece is loose debris: movable, keeping the momentum of what it was cut from.
        EnsureComp<ShuttleComponent>(newGrid.Owner);
        if (TryComp<PhysicsComponent>(oldUid, out var oldBody) && TryComp<PhysicsComponent>(newGrid.Owner, out var newBody))
        {
            _physics.SetLinearVelocity(newGrid.Owner, oldBody.LinearVelocity, body: newBody);
            _physics.SetAngularVelocity(newGrid.Owner, oldBody.AngularVelocity, body: newBody);
        }

        var ev = new GridSplitEvent(new[] { newGrid.Owner }, oldUid);
        RaiseLocalEvent(oldUid, ref ev, true);
    }

    private void ReAnchor(
        EntityUid uid,
        TransformComponent xform,
        MapGridComponent oldGrid,
        MapGridComponent newGrid,
        Vector2i tile,
        EntityUid oldGridUid,
        EntityUid newGridUid,
        TransformComponent oldGridXform,
        TransformComponent newGridXform)
    {
        if (ReAnchorMethod == null)
            throw new InvalidOperationException("SharedTransformSystem.ReAnchor was not found, the engine changed.");

        // Same tile on both grids and no rotation: the new grid takes the place of the old one.
        ReAnchorMethod.Invoke(TransformSystem,
            new object[] { uid, xform, oldGrid, newGrid, tile, tile, oldGridUid, newGridUid, oldGridXform, newGridXform, Angle.Zero });
    }
}
