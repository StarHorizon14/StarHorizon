using Content.Server.Atmos.EntitySystems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.Shipbreaking;

public sealed class GridCutsSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridCutsComponent, PostGridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<GridCutsComponent, TileChangedEvent>(OnTileChanged);
    }

    public void AddCut(EntityUid gridUid, Vector2i tile, CutEdges edge)
    {
        var cuts = EnsureComp<GridCutsComponent>(gridUid);
        var other = tile + edge.Offset();

        cuts.Cuts[tile] = cuts.Cuts.GetValueOrDefault(tile) | edge;
        cuts.Cuts[other] = cuts.Cuts.GetValueOrDefault(other) | edge.Opposite();
        Dirty(gridUid, cuts);

        _atmos.InvalidateTile(gridUid, tile);
        _atmos.InvalidateTile(gridUid, other);
    }

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
    /// A tile that is gone takes its cuts with it, on both sides of every edge, so no cut is left hanging in space.
    /// </summary>
    private void OnTileChanged(Entity<GridCutsComponent> ent, ref TileChangedEvent args)
    {
        var changed = false;

        foreach (var change in args.Changes)
        {
            if (!change.NewTile.IsEmpty)
                continue;

            var tile = change.GridIndices;
            if (!ent.Comp.Cuts.Remove(tile, out var flags))
                continue;

            changed = true;
            foreach (var edge in CutEdgesExtensions.All)
            {
                if (!flags.HasFlag(edge))
                    continue;

                var other = tile + edge.Offset();
                ClearEdge(ent.Comp, other, edge.Opposite());
                _atmos.InvalidateTile(ent.Owner, other);
            }
        }

        if (!changed)
            return;

        if (ent.Comp.Cuts.Count == 0)
            RemComp<GridCutsComponent>(ent);
        else
            Dirty(ent);
    }

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

    public void MoveCuts(EntityUid oldUid, GridCutsComponent oldCuts, EntityUid newUid, HashSet<Vector2i> moved)
    {
        var newCuts = EnsureComp<GridCutsComponent>(newUid);

        foreach (var indices in moved)
        {
            if (oldCuts.Cuts.Remove(indices, out var flags))
                newCuts.Cuts[indices] = flags;
        }

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
