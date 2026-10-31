using System.Numerics;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Horizon.Shipbreaking;

public abstract class SharedShipCutterSystem : EntitySystem
{
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly SharedMapSystem MapSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly EntityWhitelistSystem Whitelist = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<ShipCutterRotateEdgeEvent>(OnRotateEdge);
    }

    private static readonly CutEdges[] RotateOrder =
        { CutEdges.None, CutEdges.North, CutEdges.West, CutEdges.South, CutEdges.East };

    private void OnRotateEdge(ShipCutterRotateEdgeEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user ||
            !_hands.TryGetActiveItem(user, out var tool) ||
            !TryComp<ShipCutterComponent>(tool, out var cutter) ||
            cutter.Mode != ShipToolMode.Cut)
            return;

        var index = Array.IndexOf(RotateOrder, cutter.SelectedEdge);
        cutter.SelectedEdge = RotateOrder[(index + 1) % RotateOrder.Length];
        Dirty(tool.Value, cutter);
    }

    public bool AllowedGrid(ShipCutterComponent cutter, EntityUid grid)
    {
        return !cutter.RequireUtilization || HasComp<Derelict.ShuttleUtilizationComponent>(grid);
    }

    public bool AllowedGrid(EntityUid tool, EntityUid grid)
    {
        return !TryComp<ShipCutterComponent>(tool, out var cutter) || AllowedGrid(cutter, grid);
    }

    public bool TryGetSeam(MapCoordinates mapPos, float tolerance, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tile, out CutEdges edge)
    {
        edge = CutEdges.None;
        tile = default;
        grid = default!;

        if (!MapManager.TryFindGridAt(mapPos, out gridUid, out grid!))
            return false;

        var local = Vector2.Transform(mapPos.Position, TransformSystem.GetInvWorldMatrix(gridUid)) / grid.TileSize;
        tile = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));
        var fraction = local - new Vector2(tile.X, tile.Y);

        var best = tolerance;
        var bestEdge = CutEdges.None;

        if (fraction.X <= best)
        {
            best = fraction.X;
            bestEdge = CutEdges.West;
        }

        if (1f - fraction.X <= best)
        {
            best = 1f - fraction.X;
            bestEdge = CutEdges.East;
        }

        if (fraction.Y <= best)
        {
            best = fraction.Y;
            bestEdge = CutEdges.South;
        }

        if (1f - fraction.Y <= best)
        {
            best = 1f - fraction.Y;
            bestEdge = CutEdges.North;
        }

        edge = bestEdge;
        return edge != CutEdges.None;
    }

    public ShipCutterPick Pick(MapCoordinates click, ShipCutterComponent cutter)
    {
        if (!MapManager.TryFindGridAt(click, out var gridUid, out var grid) || !AllowedGrid(cutter, gridUid))
            return default;

        var local = Vector2.Transform(click.Position, TransformSystem.GetInvWorldMatrix(gridUid)) / grid.TileSize;
        var tilePos = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));

        EntityUid? wall = null;
        foreach (var anchored in MapSystem.GetAnchoredEntities((gridUid, grid), tilePos))
        {
            if (Whitelist.IsWhitelistPass(cutter.WallWhitelist, anchored))
            {
                wall = anchored;
                break;
            }
        }

        var tolerance = wall != null ? cutter.WallEdgeTolerance : cutter.EdgeTolerance;
        var hasSeam = TryGetSeam(click, tolerance, out _, out _, out var tile, out var edge);

        if (cutter.SelectedEdge != CutEdges.None)
        {
            hasSeam = true;
            tile = tilePos;
            edge = cutter.SelectedEdge;
        }

        var canCut = hasSeam && CanCut(gridUid, grid, tile, edge);

        if (canCut || (hasSeam && wall == null))
            return new ShipCutterPick(ShipCutterPickKind.Seam, gridUid, grid, tile, edge, null, canCut);

        if (wall != null)
        {
            var offset = local - new Vector2(tilePos.X + 0.5f, tilePos.Y + 0.5f);
            var wallEdge = PickWallEdge(WallHeldEdges(cutter, gridUid, grid, tilePos, wall.Value), offset);
            return new ShipCutterPick(ShipCutterPickKind.Wall, gridUid, grid, tilePos, wallEdge, wall, true);
        }

        return default;
    }

    public CutEdges WallHeldEdges(ShipCutterComponent cutter, EntityUid gridUid, MapGridComponent grid, Vector2i tile, EntityUid wall)
    {
        var cuts = TryComp<WallCutsComponent>(wall, out var wallCuts) ? wallCuts.Cuts : CutEdges.None;
        var held = CutEdges.None;

        foreach (var edge in CutEdgesExtensions.All)
        {
            if (cuts.HasFlag(edge))
                continue;

            foreach (var other in MapSystem.GetAnchoredEntities((gridUid, grid), tile + edge.Offset()))
            {
                if (other != wall && Whitelist.IsWhitelistPass(cutter.WallWhitelist, other))
                {
                    held |= edge;
                    break;
                }
            }
        }

        return held;
    }

    private static CutEdges PickWallEdge(CutEdges held, Vector2 offset)
    {
        var best = CutEdges.None;
        var bestScore = float.MinValue;

        foreach (var edge in CutEdgesExtensions.All)
        {
            if (!held.HasFlag(edge))
                continue;

            var score = offset.X * edge.Offset().X + offset.Y * edge.Offset().Y;
            if (score > bestScore)
            {
                bestScore = score;
                best = edge;
            }
        }

        return best;
    }

    public bool IsOpenEdge(MapGridComponent grid, Vector2i tile, CutEdges edge)
    {
        return MapSystem.TryGetTile(grid, tile, out var a) && !a.IsEmpty &&
               !(MapSystem.TryGetTile(grid, tile + edge.Offset(), out var b) && !b.IsEmpty);
    }

    public bool HasCut(EntityUid gridUid, Vector2i tile, CutEdges edge)
    {
        return TryComp<GridCutsComponent>(gridUid, out var cuts) && cuts.IsCut(tile, edge);
    }

    public bool CanCut(EntityUid gridUid, MapGridComponent grid, Vector2i tile, CutEdges edge)
    {
        if (!MapSystem.TryGetTile(grid, tile, out var a) || a.IsEmpty ||
            !MapSystem.TryGetTile(grid, tile + edge.Offset(), out var b) || b.IsEmpty)
            return false;

        return !HasCut(gridUid, tile, edge);
    }
}

public enum ShipCutterPickKind
{
    None,
    Seam,
    Wall,
}

public readonly record struct ShipCutterPick(
    ShipCutterPickKind Kind,
    EntityUid Grid,
    MapGridComponent? GridComp,
    Vector2i Tile,
    CutEdges Edge,
    EntityUid? Wall,
    bool CanCut);
