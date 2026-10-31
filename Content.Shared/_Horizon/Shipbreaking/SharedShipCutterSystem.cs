using System.Numerics;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Horizon.Shipbreaking;

/// <summary>
/// Finding where on a grid a <see cref="ShipCutterComponent"/> would cut. Shared so the client can show a ghost of the cut.
/// </summary>
public abstract class SharedShipCutterSystem : EntitySystem
{
    [Dependency] protected readonly IMapManager MapManager = default!;
    [Dependency] protected readonly SharedMapSystem MapSystem = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] protected readonly EntityWhitelistSystem Whitelist = default!;

    /// <summary>
    /// Finds the tile edge a point is close to.
    /// </summary>
    /// <param name="tolerance">How close to the edge the point has to be, in tiles.</param>
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

        // Not a local function: out parameters can't be used inside one.
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

    /// <summary>
    /// Works out what a click with the cutter is aimed at. Used by the server to cut and by the client to draw the ghost,
    /// so the ghost always shows what the click does.
    /// A click near the edge of a tile is the seam there, also under a wall: the gap shows over the wall.
    /// Otherwise a wall on the clicked tile is cut loose. Near an edge that can not be cut (space, already cut)
    /// it is still the seam, so the player is told that there is nothing to cut.
    /// </summary>
    public ShipCutterPick Pick(MapCoordinates click, ShipCutterComponent cutter)
    {
        if (!MapManager.TryFindGridAt(click, out var gridUid, out var grid))
            return default;

        var local = Vector2.Transform(click.Position, TransformSystem.GetInvWorldMatrix(gridUid)) / grid.TileSize;
        var tilePos = new Vector2i((int) MathF.Floor(local.X), (int) MathF.Floor(local.Y));

        // The wall and the seam are both looked for on the tile under the click.
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
        var canCut = hasSeam && CanCut(gridUid, grid, tile, edge);

        if (canCut || (hasSeam && wall == null))
            return new ShipCutterPick(ShipCutterPickKind.Seam, gridUid, grid, tile, edge, null, canCut);

        if (wall != null)
        {
            // The edge of the wall closest to the click that still holds it, none if the wall is free already.
            var offset = local - new Vector2(tilePos.X + 0.5f, tilePos.Y + 0.5f);
            var wallEdge = PickWallEdge(WallHeldEdges(cutter, gridUid, grid, tilePos, wall.Value), offset);
            return new ShipCutterPick(ShipCutterPickKind.Wall, gridUid, grid, tilePos, wallEdge, wall, true);
        }

        return default;
    }

    /// <summary>
    /// The edges of the wall by which it still holds on to other walls: those facing an anchored wall next to it
    /// and not cut yet. Floor does not hold a wall.
    /// </summary>
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

    /// <summary>
    /// Whether the edge faces open space: the tile is floor and the tile on the other side of the edge is not.
    /// Only such edges can be joined to another grid.
    /// </summary>
    public bool IsOpenEdge(MapGridComponent grid, Vector2i tile, CutEdges edge)
    {
        return MapSystem.TryGetTile(grid, tile, out var a) && !a.IsEmpty &&
               !(MapSystem.TryGetTile(grid, tile + edge.Offset(), out var b) && !b.IsEmpty);
    }

    /// <summary>
    /// Whether the given edge of the tile is cut.
    /// </summary>
    public bool HasCut(EntityUid gridUid, Vector2i tile, CutEdges edge)
    {
        return TryComp<GridCutsComponent>(gridUid, out var cuts) && cuts.IsCut(tile, edge);
    }

    /// <summary>
    /// Whether the seam can be cut: both sides have to be floor and the seam must not be cut already.
    /// </summary>
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

/// <summary>
/// What a click with the cutter is aimed at, see <see cref="SharedShipCutterSystem.Pick"/>.
/// </summary>
public readonly record struct ShipCutterPick(
    ShipCutterPickKind Kind,
    EntityUid Grid,
    MapGridComponent? GridComp,
    Vector2i Tile,
    CutEdges Edge,
    EntityUid? Wall,
    bool CanCut);
