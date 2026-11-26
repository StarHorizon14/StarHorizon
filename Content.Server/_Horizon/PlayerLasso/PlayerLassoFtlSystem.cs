using System.Numerics;
using Content.Server._Horizon.Shuttles.Events;
using Content.Server.Shuttles.Events;
using Content.Shared._Horizon.PlayerLasso.Components;
using Content.Shared._Horizon.PlayerLasso.Systems;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Horizon.PlayerLasso;

/// <summary>
/// Drags whatever is tied to a shuttle by a lasso along with it through FTL.
/// </summary>
/// <remarks>
/// FTL moves the shuttle to another map, which clears every physics joint on it and would cut the rope.
/// Right before each map change the end of the rope that is not on the shuttle is moved onto a free tile next to
/// the other end, the rope is kept tied through the map change and its joint is recreated afterwards.
/// </remarks>
public sealed class PlayerLassoFtlSystem : EntitySystem
{
    /// <summary>
    /// How far from the other end of the rope, in tiles, to look for a free tile to drop the dragged end onto.
    /// </summary>
    private const int SearchRadius = 3;

    [Dependency] private readonly PlayerLassoSystem _lasso = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private readonly List<(Entity<PlayerLassoComponent> Lasso, EntityUid ItemEnd, EntityUid Anchor)> _ropes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FTLGridMapChangingEvent>(OnGridMapChanging);
        SubscribeLocalEvent<FTLStartedEvent>(OnFtlStarted);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFtlCompleted);
    }

    private void OnGridMapChanging(ref FTLGridMapChangingEvent ev)
    {
        var grid = ev.Grid;

        if (!TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        _ropes.Clear();

        var query = EntityQueryEnumerator<PlayerLassoComponent>();
        while (query.MoveNext(out var item, out var lasso))
        {
            if (lasso.Anchor is not { } anchor || lasso.InTransit)
                continue;

            _ropes.Add(((item, lasso), _lasso.GetItemEnd(item), anchor));
        }

        foreach (var (lasso, itemEnd, anchor) in _ropes)
        {
            var itemOnGrid = IsOnGrid(itemEnd, grid);
            var anchorOnGrid = IsOnGrid(anchor, grid);

            if (!itemOnGrid && !anchorOnGrid)
                continue;

            // If the off-shuttle end can't be dragged along, the map change simply cuts the rope.
            if (!itemOnGrid && !TryDragOnto(itemEnd, anchor, (grid, gridComp)))
                continue;

            if (!anchorOnGrid && !TryDragOnto(anchor, itemEnd, (grid, gridComp)))
                continue;

            _lasso.BeginTransit(lasso);
        }

        _ropes.Clear();
    }

    private void OnFtlStarted(ref FTLStartedEvent ev)
    {
        EndAllTransits();
    }

    private void OnFtlCompleted(ref FTLCompletedEvent ev)
    {
        EndAllTransits();
    }

    /// <summary>
    /// FTL raises the map-changing event, moves the grid and raises started/completed within one call,
    /// so every rope in transit at this point belongs to the shuttle that has just moved.
    /// </summary>
    private void EndAllTransits()
    {
        var query = EntityQueryEnumerator<PlayerLassoComponent>();
        while (query.MoveNext(out var item, out var lasso))
        {
            if (lasso.InTransit)
                _lasso.EndTransit((item, lasso));
        }
    }

    private bool IsOnGrid(EntityUid uid, EntityUid grid)
    {
        return Transform(uid).GridUid == grid && uid != grid;
    }

    /// <summary>
    /// Moves <paramref name="mover"/> onto the shuttle, to the free tile closest to it near <paramref name="target"/>.
    /// </summary>
    private bool TryDragOnto(EntityUid mover, EntityUid target, Entity<MapGridComponent> grid)
    {
        if (HasComp<MapGridComponent>(mover) || HasComp<MapComponent>(mover) || _container.IsEntityInContainer(mover))
            return false;

        // A braced victim holds their ground, but a jumping shuttle is stronger.
        _lasso.ReleaseBrace(mover);

        var moverXform = Transform(mover);

        if (moverXform.Anchored ||
            !TryComp<PhysicsComponent>(mover, out var moverPhysics) ||
            moverPhysics.BodyType == BodyType.Static)
        {
            return false;
        }

        if (!TryFindFreeTile(grid, target, _transform.GetWorldPosition(moverXform), out var coordinates))
            return false;

        _transform.SetCoordinates(mover, moverXform, coordinates);
        _physics.SetLinearVelocity(mover, Vector2.Zero, body: moverPhysics);
        _physics.SetAngularVelocity(mover, 0f, body: moverPhysics);
        return true;
    }

    private bool TryFindFreeTile(Entity<MapGridComponent> grid, EntityUid target, Vector2 moverWorldPos, out EntityCoordinates coordinates)
    {
        coordinates = default;

        var center = _map.TileIndicesFor(grid, grid.Comp, Transform(target).Coordinates);
        var gridInvMatrix = _transform.GetInvWorldMatrix(grid);
        var moverLocalPos = Vector2.Transform(moverWorldPos, gridInvMatrix);

        // A mob stands on its own tile, so the dragged end may share it; a structure occupies it.
        var startRadius = HasComp<MobStateComponent>(target) ? 0 : 1;

        for (var radius = startRadius; radius <= SearchRadius; radius++)
        {
            var found = false;
            var bestDistance = float.MaxValue;

            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    // Only the ring at this radius, the inner tiles were checked already.
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;

                    var indices = center + new Vector2i(x, y);

                    if (!_map.TryGetTileRef(grid, grid.Comp, indices, out var tile) ||
                        _turf.IsSpace(tile) ||
                        _turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                    {
                        continue;
                    }

                    var tileCenter = _map.GridTileToLocal(grid, grid.Comp, indices);
                    var distance = (tileCenter.Position - moverLocalPos).LengthSquared();

                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    coordinates = tileCenter;
                    found = true;
                }
            }

            if (found)
                return true;
        }

        return false;
    }
}
