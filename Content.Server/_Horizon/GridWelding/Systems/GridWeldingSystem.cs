using System.Numerics;
using Content.Server._Horizon.Shipbreaking;
using Content.Server.Administration.Logs;
using Content.Server.PowerCell;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Horizon.GridWelding.Components;
using Content.Shared._Horizon.GridWelding.Events;
using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.Database;
using Content.Shared.Gravity;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Robust.Server.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.GridWelding.Systems;

/// <summary>
/// Handles the grid welding tool: picking two points on two different grids, merging them into one
/// via <see cref="GridFixtureSystem.Merge"/>, and tracking/sealing the resulting unwelded seam.
/// Server-only - merging grids and the validation that guards it must not be client-predicted.
/// </summary>
public sealed partial class GridWeldingSystem : SharedGridWeldingSystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GridFixtureSystem _gridFixtures = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GridCutsSystem _cuts = default!;
    [Dependency] private readonly SharedShipCutterSystem _cutter = default!;
    [Dependency] private readonly GridGravitySystem _gridGravity = default!;
    [Dependency] private readonly DockingSystem _docking = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SeamWelderComponent, ShipToolModeChangedEvent>(OnModeChanged);
        SubscribeLocalEvent<SeamWelderComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SeamWelderComponent, GridWeldMergeDoAfterEvent>(OnMergeDoAfter);
        SubscribeLocalEvent<SeamWelderComponent, GridWeldSealDoAfterEvent>(OnSealDoAfter);
    }

    // Switching the tool away from welding drops the edge that was picked.
    private void OnModeChanged(Entity<SeamWelderComponent> ent, ref ShipToolModeChangedEvent args)
    {
        if (!ent.Comp.HasPendingSelection)
            return;

        ClearPendingSelection(ent.Comp);
        Dirty(ent);
    }

    private bool IsWelding(EntityUid tool)
    {
        return TryComp<ShipCutterComponent>(tool, out var mode) && mode.Mode == ShipToolMode.Weld;
    }

    /// <summary>
    /// Starts a do-after on the tool itself, if the power cell has the charge.
    /// </summary>
    private void StartWeld(Entity<SeamWelderComponent> ent, EntityUid user, TimeSpan delay, float charge, DoAfterEvent ev)
    {
        if (!_cell.HasCharge(ent, charge, user: user))
        {
            _popup.PopupEntity(Loc.GetString("ship-cutter-no-charge"), ent, user);
            return;
        }

        var args = new DoAfterArgs(EntityManager, user, delay, ev, ent, target: ent, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void OnAfterInteract(Entity<SeamWelderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !IsWelding(ent))
            return;

        var comp = ent.Comp;
        var clickCoords = _xform.ToMapCoordinates(args.ClickLocation);

        if (!_mapManager.TryFindGridAt(clickCoords, out var gridUid, out var grid))
            return;

        if (HasComp<MapComponent>(gridUid))
        {
            _popup.PopupEntity(Loc.GetString("grid-welding-invalid-target"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        var tile = _maps.TileIndicesFor((gridUid, grid), clickCoords);
        if (!_maps.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
        {
            _popup.PopupEntity(Loc.GetString("grid-welding-no-tile"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        // Sealing a cut is a single click on the seam - no two-step pick needed.
        if (_cutter.TryGetSeam(clickCoords, comp.SeamTolerance, out var seamGrid, out _, out var seamTile, out var seamEdge) &&
            _cutter.HasCut(seamGrid, seamTile, seamEdge))
        {
            StartSeal(ent, args.User, seamGrid, seamTile, seamEdge, comp);
            return;
        }

        // Welding two grids joins an edge of one with an edge of the other: the click picks the nearest edge of the tile.
        if (!_cutter.TryGetSeam(clickCoords, comp.EdgeSelectTolerance, out _, out _, out var edgeTile, out var edge))
            return;

        // Only an edge that faces open space can be joined to something.
        if (!_cutter.IsOpenEdge(grid, edgeTile, edge))
        {
            _popup.PopupEntity(Loc.GetString("grid-welding-edge-not-open"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (!comp.HasPendingSelection || comp.PendingSelectionExpiry is not { } expiry || _timing.CurTime > expiry)
        {
            comp.HasPendingSelection = true;
            comp.PendingGrid = GetNetEntity(gridUid);
            comp.PendingTile = edgeTile;
            comp.PendingEdge = edge;
            comp.PendingSelectionExpiry = _timing.CurTime + comp.SelectionTimeout;
            Dirty(ent);
            return;
        }

        var gridA = GetEntity(comp.PendingGrid!.Value);
        var tileA = comp.PendingTile;
        var edgeA = comp.PendingEdge;

        // Clear the pending selection up front - any failure past this point resets it, rather than
        // leaving the tool armed against a target we already know is bad.
        ClearPendingSelection(comp);
        Dirty(ent);

        if (!TryValidateWeld(comp, args.User, gridA, tileA, edgeA, gridUid, edgeTile, edge, out _, out _, out var errorKey))
        {
            if (errorKey != null)
                _popup.PopupEntity(Loc.GetString(errorKey), ent, args.User, PopupType.SmallCaution);
            _audio.PlayPvs(comp.ErrorSound, ent);
            return;
        }

        var ev = new GridWeldMergeDoAfterEvent(GetNetEntity(gridA), tileA, edgeA, GetNetEntity(gridUid), edgeTile, edge);
        StartWeld(ent, args.User, comp.WeldMergeDelay, comp.MergeCharge, ev);
    }

    private void StartSeal(Entity<SeamWelderComponent> ent, EntityUid user, EntityUid gridUid, Vector2i tile, CutEdges edge, SeamWelderComponent comp)
    {
        var ev = new GridWeldSealDoAfterEvent(GetNetEntity(gridUid), tile, edge);
        StartWeld(ent, user, comp.WeldSealDelay, comp.SealCharge, ev);
    }

    private void OnMergeDoAfter(Entity<SeamWelderComponent> ent, ref GridWeldMergeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var gridAUid = GetEntity(args.GridA);
        var gridBUid = GetEntity(args.GridB);

        if (!TryValidateWeld(ent.Comp, args.User, gridAUid, args.TileA, args.EdgeA, gridBUid, args.TileB, args.EdgeB, out var offset, out var rotation, out var errorKey))
        {
            if (errorKey != null)
                _popup.PopupEntity(Loc.GetString(errorKey), ent, args.User, PopupType.SmallCaution);
            _audio.PlayPvs(ent.Comp.ErrorSound, ent);
            return;
        }

        if (!_cell.TryUseCharge(ent, ent.Comp.MergeCharge, user: args.User))
        {
            _popup.PopupEntity(Loc.GetString("ship-cutter-no-charge"), ent, args.User);
            return;
        }

        MergeGrids(gridAUid, gridBUid, offset, rotation, args.TileA, args.TileB);

        _audio.PlayPvs(ent.Comp.WeldSound, ent);

        _adminLogger.Add(LogType.Construction, LogImpact.High,
            $"{ToPrettyString(args.User):player} welded {ToPrettyString(gridBUid):grid} into {ToPrettyString(gridAUid):grid}");
    }

    /// <summary>
    /// Merges grid B into grid A at the given offset and rotation (from <see cref="TryValidateWeld"/>). Every border
    /// between the two grids, besides the welded pair of tiles, is left as a cut that has to be welded shut.
    /// </summary>
    public void MergeGrids(EntityUid gridAUid, EntityUid gridBUid, Vector2i offset, Angle rotation, Vector2i tileA, Vector2i tileB)
    {
        var gridA = Comp<MapGridComponent>(gridAUid);
        var gridB = Comp<MapGridComponent>(gridBUid);
        var xformA = Transform(gridAUid);
        var xformB = Transform(gridBUid);

        // Snapshot both footprints before Merge mutates grid A and deletes grid B outright.
        var gridBTiles = new List<TileRef>(_maps.GetAllTiles(gridBUid, gridB));
        var gridAFootprint = new HashSet<Vector2i>();
        foreach (var tile in _maps.GetAllTiles(gridAUid, gridA))
            gridAFootprint.Add(tile.GridIndices);

        // Grid B is deleted by the merge, take its cuts with us first.
        var gridBCuts = TryComp<GridCutsComponent>(gridBUid, out var bCuts)
            ? new Dictionary<Vector2i, CutEdges>(bCuts.Cuts)
            : null;

        // The gravity of grid B goes with it too.
        var bInherentGravity = false;
        var bGravityEnabled = false;
        if (TryComp<GravityComponent>(gridBUid, out var bGravity))
        {
            bInherentGravity = bGravity.Inherent;
            bGravityEnabled = bGravity.Enabled;
        }

        ReleaseStrays(gridBUid, gridB, xformB);
        UndockBetween(gridAUid, gridBUid);

        gridA.CanSplit = false;
        _gridFixtures.Merge(gridAUid, gridBUid, offset, rotation, gridA, gridB, xformA, xformB);
        gridA.CanSplit = true;
        _gridFixtures.CheckSplits(gridAUid);

        // The generators of grid B now stand on grid A, which has to count them.
        _gridGravity.AfterMerge(gridAUid, bInherentGravity, bGravityEnabled);

        ApplyMergeSeams(gridAUid, gridA, gridAFootprint, gridBTiles, gridBCuts, offset, rotation, tileA, tileB);
    }

    /// <summary>
    /// Two grids that are docked to each other (a bought wreck is docked to the station) have their dock ports on one grid
    /// after the merge, and re-docking a port with the one it is docked with then joins a body to itself.
    /// The ports between the two grids are undocked first.
    /// </summary>
    private void UndockBetween(EntityUid gridAUid, EntityUid gridBUid)
    {
        var toUndock = new List<Entity<DockingComponent>>();
        var docks = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (docks.MoveNext(out var uid, out var dock, out var xform))
        {
            if (xform.GridUid != gridBUid || dock.DockedWith is not { } other)
                continue;

            if (Transform(other).GridUid == gridAUid)
                toUndock.Add((uid, dock));
        }

        foreach (var dock in toUndock)
        {
            _docking.Undock(dock);
        }
    }

    /// <summary>
    /// The merge moves only what stands inside the tiles of grid B and deletes the grid, and the engine asserts that nothing
    /// is left on it. Whatever hangs on the grid outside of its tiles (a thing that drifted off the edge, a cut loose
    /// part) is put on the map where it is, so it is neither lost nor in the way.
    /// </summary>
    private void ReleaseStrays(EntityUid gridBUid, MapGridComponent gridB, TransformComponent xformB)
    {
        if (xformB.MapUid is not { } map)
            return;

        var strays = new List<EntityUid>();
        var children = xformB.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            var xform = Transform(child);
            if (xform.Anchored)
                continue;

            var local = xform.LocalPosition;
            var indices = _maps.LocalToTile(gridBUid, gridB, xform.Coordinates);
            var bounds = Box2.FromDimensions(new Vector2(indices.X, indices.Y) * gridB.TileSize, new Vector2(gridB.TileSize));
            if (!_maps.TryGetTile(gridB, indices, out var tile) || tile.IsEmpty || !bounds.Contains(local))
                strays.Add(child);
        }

        foreach (var stray in strays)
        {
            var xform = Transform(stray);
            _xform.SetCoordinates(stray, xform, new EntityCoordinates(map, _xform.GetWorldPosition(xform)),
                _xform.GetWorldRotation(xform));
        }
    }

    private void OnSealDoAfter(Entity<SeamWelderComponent> ent, ref GridWeldSealDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var gridUid = GetEntity(args.Grid);
        if (!_cuts.RemoveCut(gridUid, args.Tile, args.Edge))
            return;

        // The cut is welded shut, the charge is taken for it.
        _cell.TryUseCharge(ent, ent.Comp.SealCharge, user: args.User);

        _audio.PlayPvs(ent.Comp.SealSound, ent);

        _adminLogger.Add(LogType.Construction, LogImpact.Low,
            $"{ToPrettyString(args.User):player} welded a seam shut on {ToPrettyString(gridUid):grid}");
    }

    private static void ClearPendingSelection(SeamWelderComponent comp)
    {
        comp.HasPendingSelection = false;
        comp.PendingGrid = null;
        comp.PendingEdge = CutEdges.None;
        comp.PendingSelectionExpiry = null;
    }
}
