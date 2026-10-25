using System.Numerics;
using Content.Shared._Horizon.GridWelding.Components;
using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._Horizon.Shipbreaking;

/// <summary>
/// While a <see cref="ShipCutterComponent"/> is held, shows where a click would cut, the way the entity
/// placement ghost works in the admin spawn menu: green where a cut can be made, red where it can not.
/// While a <see cref="SeamWelderComponent"/> is held, shows the cut that would be welded shut, or the edge
/// that a click picks for joining two grids, the edge already picked, and whether the two can be joined.
/// </summary>
public sealed class ShipCutterGhostOverlay : Overlay
{
    private static readonly Color ValidColor = Color.Lime.WithAlpha(0.5f);
    private static readonly Color InvalidColor = Color.Red.WithAlpha(0.5f);
    private static readonly Color PickColor = Color.Cyan.WithAlpha(0.5f);
    private static readonly Color SelectedColor = Color.Orange.WithAlpha(0.5f);

    // The ghost is drawn a bit wider than the cut itself, so it can be seen.
    private const float GhostWidthPixels = GridCutsComponent.CutWidthPixels * 2f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly IEntityManager _entManager;
    private readonly SharedHandsSystem _hands;
    private readonly SharedShipCutterSystem _cutter;
    private readonly SharedGridWeldingSystem _welding;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;

    public ShipCutterGhostOverlay(IEntityManager entManager)
    {
        IoCManager.InjectDependencies(this);

        _entManager = entManager;
        _hands = entManager.System<SharedHandsSystem>();
        _cutter = entManager.System<SharedShipCutterSystem>();
        _welding = entManager.System<SharedGridWeldingSystem>();
        _map = entManager.System<SharedMapSystem>();
        _transform = entManager.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player || !_hands.TryGetActiveItem(player, out var held))
            return;

        var mouse = _input.MouseScreenPosition;
        if (!mouse.IsValid)
            return;

        var mapPos = _eye.PixelToMap(mouse);
        if (mapPos.MapId != args.MapId)
            return;

        var handle = args.WorldHandle;

        if (_entManager.TryGetComponent(held, out BreachChargeComponent? charge))
        {
            DrawCharge(handle, charge, mapPos);
            handle.SetTransform(Matrix3x2.Identity);
            return;
        }

        if (!_entManager.TryGetComponent(held, out ShipCutterComponent? cutter))
            return;

        if (cutter.Mode == ShipToolMode.Cut)
        {
            // The cutter shows where it would cut.
            var pick = _cutter.Pick(mapPos, cutter);
            if (pick.Kind == ShipCutterPickKind.Seam)
                DrawEdge(handle, pick.Grid, pick.GridComp!, pick.Tile, pick.Edge, pick.CanCut ? ValidColor : InvalidColor);
            else if (pick.Kind == ShipCutterPickKind.Wall && pick.Edge != CutEdges.None)
                DrawEdge(handle, pick.Grid, pick.GridComp!, pick.Tile, pick.Edge, ValidColor);
            else if (pick.Kind == ShipCutterPickKind.Wall)
                DrawTile(handle, pick.Grid, pick.GridComp!, pick.Tile, ValidColor); // free already, comes loose
        }
        else if (_entManager.TryGetComponent(held, out SeamWelderComponent? welder))
        {
            DrawWelding(handle, welder, mapPos);
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    /// <summary>
    /// The line of seams a charge would cut if it was hung on the edge under the cursor: green where a seam can be cut,
    /// red where it can not.
    /// </summary>
    private void DrawCharge(DrawingHandleWorld handle, BreachChargeComponent charge, MapCoordinates mapPos)
    {
        if (!_cutter.TryGetSeam(mapPos, charge.EdgeTolerance, out var gridUid, out var grid, out var tile, out var edge))
            return;

        var along = edge.Along();
        var first = -(charge.Segments - 1) / 2;
        for (var k = 0; k < charge.Segments; k++)
        {
            var segment = tile + along * (first + k);
            DrawEdge(handle, gridUid, grid, segment, edge, _cutter.CanCut(gridUid, grid, segment, edge) ? ValidColor : InvalidColor);
        }
    }

    private void DrawWelding(DrawingHandleWorld handle, SeamWelderComponent welder, MapCoordinates mapPos)
    {
        // A cut under the cursor is what a click would weld shut.
        if (_cutter.TryGetSeam(mapPos, welder.SeamTolerance, out var sealGrid, out var sealGridComp, out var sealTile, out var sealEdge) &&
            _cutter.HasCut(sealGrid, sealTile, sealEdge))
        {
            DrawEdge(handle, sealGrid, sealGridComp, sealTile, sealEdge, ValidColor);
            return;
        }

        // The edge of the first grid that is already picked.
        EntityUid? firstGrid = null;
        MapGridComponent? firstGridComp = null;
        if (welder.HasPendingSelection &&
            (welder.PendingSelectionExpiry is not { } expiry || _timing.CurTime <= expiry) &&
            welder.PendingGrid is { } pendingGrid &&
            _entManager.TryGetEntity(pendingGrid, out var pendingUid) &&
            _entManager.TryGetComponent(pendingUid, out firstGridComp))
        {
            firstGrid = pendingUid;
            DrawEdge(handle, pendingUid.Value, firstGridComp, welder.PendingTile, welder.PendingEdge, SelectedColor);
        }

        // The edge a click would pick now.
        if (!_cutter.TryGetSeam(mapPos, welder.EdgeSelectTolerance, out var gridUid, out var grid, out var tile, out var edge) ||
            !_cutter.IsOpenEdge(grid, tile, edge))
            return;

        if (firstGrid == null || firstGridComp == null)
        {
            DrawEdge(handle, gridUid, grid, tile, edge, PickColor);
            return;
        }

        // Picking the other grid: show whether the two edges can be joined, and a line between them.
        if (gridUid == firstGrid)
        {
            DrawEdge(handle, gridUid, grid, tile, edge, InvalidColor);
            return;
        }

        var valid = _welding.TryComputeWeldGeometry(
            firstGrid.Value, firstGridComp, _entManager.GetComponent<TransformComponent>(firstGrid.Value), welder.PendingTile, welder.PendingEdge,
            gridUid, grid, _entManager.GetComponent<TransformComponent>(gridUid), tile, edge,
            welder.TileProximityTolerance, welder.RotationSnapTolerance,
            out _, out _, out _, out _);

        var color = valid ? ValidColor : InvalidColor;
        DrawEdge(handle, gridUid, grid, tile, edge, color);

        var from = EdgeCenter(firstGrid.Value, firstGridComp, welder.PendingTile, welder.PendingEdge);
        var to = EdgeCenter(gridUid, grid, tile, edge);
        handle.DrawLine(from, to, color);
    }

    private Vector2 EdgeCenter(EntityUid gridUid, MapGridComponent grid, Vector2i tile, CutEdges edge)
    {
        var local = edge.GetRect(tile, grid.TileSize, 0f).Center;
        return Vector2.Transform(local, _transform.GetWorldMatrix(gridUid));
    }

    private void DrawEdge(DrawingHandleWorld handle, EntityUid gridUid, MapGridComponent grid, Vector2i tile, CutEdges edge, Color color)
    {
        var half = GhostWidthPixels / 32f / 2f * grid.TileSize;

        handle.SetTransform(_transform.GetWorldMatrix(gridUid));
        handle.DrawRect(edge.GetRect(tile, grid.TileSize, half), color);
        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTile(DrawingHandleWorld handle, EntityUid gridUid, MapGridComponent grid, Vector2i tile, Color color)
    {
        handle.SetTransform(_transform.GetWorldMatrix(gridUid));
        handle.DrawRect(Box2.FromDimensions(new Vector2(tile.X, tile.Y) * grid.TileSize, new Vector2(grid.TileSize)), color);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
