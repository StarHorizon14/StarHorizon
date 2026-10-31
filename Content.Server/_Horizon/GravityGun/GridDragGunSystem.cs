using System.Numerics;
using Content.Server.PowerCell;
using Content.Shared._Horizon.GravityGun;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._Horizon.GravityGun;

/// <summary>
/// Server side of <see cref="GridDragGunComponent"/>, copied from the griddrag command: the grid is put where the client
/// asks, as long as the user holds the gun, has charge, and the grabbed point is in reach.
/// </summary>
public sealed class GridDragGunSystem : EntitySystem
{
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<ICommonSession, (EntityUid Grid, Vector2 Local)> _dragging = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<GridDragGunStartRequest>(OnStart);
        SubscribeNetworkEvent<GridDragGunPositionRequest>(OnPosition);
        SubscribeNetworkEvent<GridDragGunStopRequest>(OnStop);
    }

    private bool TryGetGun(ICommonSession session, out Entity<GridDragGunComponent> gun, out EntityUid user)
    {
        gun = default;
        user = default;

        if (session.AttachedEntity is not { } attached ||
            !_hands.TryGetActiveItem(attached, out var item) ||
            !TryComp<GridDragGunComponent>(item, out var comp) ||
            !_cell.HasDrawCharge(item.Value, user: attached))
            return false;

        user = attached;
        gun = (item.Value, comp);
        return true;
    }

    private void OnStart(GridDragGunStartRequest msg, EntitySessionEventArgs args)
    {
        var grid = GetEntity(msg.Grid);

        if (!TryGetGun(args.SenderSession, out var gun, out var user) || !HasComp<PhysicsComponent>(grid) ||
            Transform(user).GridUid == grid)
            return;

        var point = Vector2.Transform(msg.LocalPoint, _transform.GetWorldMatrix(grid));
        if ((point - _transform.GetWorldPosition(user)).Length() > gun.Comp.MaxDistance)
            return;

        _dragging[args.SenderSession] = (grid, msg.LocalPoint);
        _physics.SetLinearVelocity(grid, Vector2.Zero);
        _physics.SetAngularVelocity(grid, 0f);
    }

    private void OnPosition(GridDragGunPositionRequest msg, EntitySessionEventArgs args)
    {
        var grid = GetEntity(msg.Grid);

        if (!_dragging.TryGetValue(args.SenderSession, out var state) || state.Grid != grid || TerminatingOrDeleted(grid) ||
            !TryGetGun(args.SenderSession, out var gun, out var user))
        {
            _dragging.Remove(args.SenderSession);
            return;
        }

        // The point that is held has to stay in reach of the user.
        var point = msg.WorldPosition + _transform.GetWorldRotation(grid).RotateVec(state.Local);
        if ((point - _transform.GetWorldPosition(user)).Length() > gun.Comp.MaxDistance)
            return;

        _transform.SetWorldPosition(grid, msg.WorldPosition);
    }

    private void OnStop(GridDragGunStopRequest msg, EntitySessionEventArgs args)
    {
        var grid = GetEntity(msg.Grid);

        if (!_dragging.Remove(args.SenderSession, out var state) || state.Grid != grid || TerminatingOrDeleted(grid))
            return;

        _physics.SetLinearVelocity(grid, msg.LinearVelocity);
        _physics.SetAngularVelocity(grid, 0f);
    }
}
