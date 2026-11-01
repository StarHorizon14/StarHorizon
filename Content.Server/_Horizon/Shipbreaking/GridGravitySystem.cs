using Content.Server.Gravity;
using Content.Shared.Gravity;
using Robust.Server.Physics;

namespace Content.Server._Horizon.Shipbreaking;

public sealed class GridGravitySystem : EntitySystem
{
    [Dependency] private readonly GravitySystem _gravity = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        TryComp<GravityComponent>(args.Grid, out var oldGravity);

        foreach (var newGrid in args.NewGrids)
        {
            if (oldGravity is { Inherent: true } && TryComp<GravityComponent>(newGrid, out var newGravity))
            {
                newGravity.Inherent = true;
                newGravity.Enabled = oldGravity.Enabled;
                Dirty(newGrid, newGravity);
            }

            _gravity.RefreshGravity(newGrid);
        }

        _gravity.RefreshGravity(args.Grid);
    }

    public void AfterMerge(EntityUid gridA, bool bInherent, bool bEnabled)
    {
        if (bInherent && TryComp<GravityComponent>(gridA, out var gravity))
        {
            gravity.Inherent = true;
            gravity.Enabled |= bEnabled;
            Dirty(gridA, gravity);
        }

        _gravity.RefreshGravity(gridA);
    }
}
