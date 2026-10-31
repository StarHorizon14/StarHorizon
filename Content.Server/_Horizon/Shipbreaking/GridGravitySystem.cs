using Content.Server.Gravity;
using Content.Shared.Gravity;
using Robust.Server.Physics;

namespace Content.Server._Horizon.Shipbreaking;

/// <summary>
/// Keeps gravity right when grids are split or welded together. Gravity generators are moved between grids
/// with the engine's ReAnchor, which does not tell the generator its parent changed, so nothing would
/// recalculate gravity on either grid, and a new grid starts without it.
/// </summary>
public sealed class GridGravitySystem : EntitySystem
{
    [Dependency] private readonly GravitySystem _gravity = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Also covers the engine's own splits (damage), where a generator can end up on the new grid.
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        TryComp<GravityComponent>(args.Grid, out var oldGravity);

        foreach (var newGrid in args.NewGrids)
        {
            // Constant gravity (planets, mapped stations) goes with every part of the grid.
            if (oldGravity is { Inherent: true } && TryComp<GravityComponent>(newGrid, out var newGravity))
            {
                newGravity.Inherent = true;
                newGravity.Enabled = oldGravity.Enabled;
                Dirty(newGrid, newGravity);
            }

            // The generators have already been moved: count them again on both sides.
            _gravity.RefreshGravity(newGrid);
        }

        _gravity.RefreshGravity(args.Grid);
    }

    /// <summary>
    /// Call after a grid was welded into <paramref name="gridA"/>. <paramref name="bInherent"/> and
    /// <paramref name="bEnabled"/> are the gravity of the grid that was merged in (it no longer exists).
    /// </summary>
    public void AfterMerge(EntityUid gridA, bool bInherent, bool bEnabled)
    {
        if (bInherent && TryComp<GravityComponent>(gridA, out var gravity))
        {
            gravity.Inherent = true;
            gravity.Enabled |= bEnabled;
            Dirty(gridA, gravity);
        }

        // The generators of the merged-in grid now stand on this one.
        _gravity.RefreshGravity(gridA);
    }
}
