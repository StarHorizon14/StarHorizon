using Content.Server.Atmos.EntitySystems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.Shipbreaking;

/// <summary>
/// Adds, removes and moves the cuts stored in <see cref="GridCutsComponent"/>, keeping atmos in sync.
/// Shared by the ship cutter (cuts a seam), the grid welder (seals one, and creates them where grids are welded)
/// and grid splitting.
/// </summary>
public sealed class GridCutsSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        // The engine split a grid on its own (damage), so the cuts of the part that left have to follow it.
        SubscribeLocalEvent<GridCutsComponent, PostGridSplitEvent>(OnGridSplit);
    }

    /// <summary>
    /// Cuts the given edge of the tile. The same cut is stored on the tile on the other side.
    /// </summary>
    public void AddCut(EntityUid gridUid, Vector2i tile, CutEdges edge)
    {
        var cuts = EnsureComp<GridCutsComponent>(gridUid);
        var other = tile + edge.Offset();

        cuts.Cuts[tile] = cuts.Cuts.GetValueOrDefault(tile) | edge;
        cuts.Cuts[other] = cuts.Cuts.GetValueOrDefault(other) | edge.Opposite();
        Dirty(gridUid, cuts);

        // The tiles on both sides now leak to space, see AtmosphereSystem.UpdateTileData.
        _atmos.InvalidateTile(gridUid, tile);
        _atmos.InvalidateTile(gridUid, other);
    }

    /// <summary>
    /// Welds a cut shut, whichever side of it holds the record. Removes the component once the grid has no cuts left.
    /// </summary>
    /// <returns>True if there was a cut to seal.</returns>
    public bool RemoveCut(EntityUid gridUid, Vector2i tile, CutEdges edge)
    {
        if (!TryComp<GridCutsComponent>(gridUid, out var cuts) || !cuts.IsCut(tile, edge))
            return false;

        var other = tile + edge.Offset();
        ClearEdge(cuts, tile, edge);
        ClearEdge(cuts, other, edge.Opposite());

        _atmos.InvalidateTile(gridUid, tile);
        _atmos.InvalidateTile(gridUid, other);

        if (cuts.Cuts.Count == 0)
            RemComp<GridCutsComponent>(gridUid);
        else
            Dirty(gridUid, cuts);

        return true;
    }

    /// <summary>
    /// Whether the grid still has any unsealed cuts.
    /// </summary>
    public bool HasCuts(EntityUid gridUid)
    {
        return TryComp<GridCutsComponent>(gridUid, out var cuts) && cuts.Cuts.Count > 0;
    }

    private static void ClearEdge(GridCutsComponent cuts, Vector2i tile, CutEdges edge)
    {
        if (!cuts.Cuts.TryGetValue(tile, out var flags))
            return;

        flags &= ~edge;
        if (flags == CutEdges.None)
            cuts.Cuts.Remove(tile);
        else
            cuts.Cuts[tile] = flags;
    }

    /// <summary>
    /// Moves the cuts of the moved part to the new grid. The cuts along the split stay on both halves, as gaps on
    /// their edges.
    /// </summary>
    public void MoveCuts(EntityUid oldUid, GridCutsComponent oldCuts, EntityUid newUid, HashSet<Vector2i> moved)
    {
        var newCuts = EnsureComp<GridCutsComponent>(newUid);

        foreach (var indices in moved)
        {
            if (oldCuts.Cuts.Remove(indices, out var flags))
                newCuts.Cuts[indices] = flags;
        }

        // Tiles with cuts leak to space, see AtmosphereSystem.UpdateTileData: both grids have to look at them again.
        foreach (var tile in newCuts.Cuts.Keys)
        {
            _atmos.InvalidateTile(newUid, tile);
        }

        foreach (var tile in oldCuts.Cuts.Keys)
        {
            _atmos.InvalidateTile(oldUid, tile);
        }

        if (newCuts.Cuts.Count == 0)
            RemComp<GridCutsComponent>(newUid);
        else
            Dirty(newUid, newCuts);

        if (oldCuts.Cuts.Count == 0)
            RemComp<GridCutsComponent>(oldUid);
        else
            Dirty(oldUid, oldCuts);
    }

    private void OnGridSplit(Entity<GridCutsComponent> ent, ref PostGridSplitEvent args)
    {
        if (!TryComp<MapGridComponent>(ent, out var oldGrid) || !TryComp<MapGridComponent>(args.Grid, out var newGrid))
            return;

        // The engine split the grid on its own: its old tiles are already cleared here. (On the cutter path the tiles
        // are cleared after the event and ShipCutterSystem moves the cuts itself.)
        // Tiles that are gone from the old grid but exist on the new one went along with the split.
        var moved = new HashSet<Vector2i>();
        foreach (var tile in ent.Comp.Cuts.Keys)
        {
            if (_map.TryGetTile(oldGrid, tile, out var oldTile) && !oldTile.IsEmpty)
                continue;

            if (_map.TryGetTile(newGrid, tile, out var newTile) && !newTile.IsEmpty)
                moved.Add(tile);
        }

        if (moved.Count > 0)
            MoveCuts(ent, ent.Comp, args.Grid, moved);
    }
}
