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

        if (!_mapManager.TryFindGridAt(clickCoords, out var gridUid, out var grid) || !_cutter.AllowedGrid(ent, gridUid))
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

        if (_cutter.TryGetSeam(clickCoords, comp.SeamTolerance, out var seamGrid, out _, out var seamTile, out var seamEdge) &&
            _cutter.HasCut(seamGrid, seamTile, seamEdge))
        {
            StartSeal(ent, args.User, seamGrid, seamTile, seamEdge, comp);
            return;
        }

        if (!_cutter.TryGetSeam(clickCoords, comp.EdgeSelectTolerance, out _, out _, out var edgeTile, out var edge))
            return;

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

    public void MergeGrids(EntityUid gridAUid, EntityUid gridBUid, Vector2i offset, Angle rotation, Vector2i tileA, Vector2i tileB)
    {
        var gridA = Comp<MapGridComponent>(gridAUid);
        var gridB = Comp<MapGridComponent>(gridBUid);
        var xformA = Transform(gridAUid);
        var xformB = Transform(gridBUid);

        var gridBTiles = new List<TileRef>(_maps.GetAllTiles(gridBUid, gridB));
        var gridAFootprint = new HashSet<Vector2i>();
        foreach (var tile in _maps.GetAllTiles(gridAUid, gridA))
            gridAFootprint.Add(tile.GridIndices);

        var gridBCuts = TryComp<GridCutsComponent>(gridBUid, out var bCuts)
            ? new Dictionary<Vector2i, CutEdges>(bCuts.Cuts)
            : null;

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

        _gridGravity.AfterMerge(gridAUid, bInherentGravity, bGravityEnabled);

        ApplyMergeSeams(gridAUid, gridA, gridAFootprint, gridBTiles, gridBCuts, offset, rotation, tileA, tileB);
    }

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
