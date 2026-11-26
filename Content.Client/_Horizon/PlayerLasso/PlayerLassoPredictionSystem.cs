using Content.Shared._Horizon.PlayerLasso.Components;
using Content.Shared._Horizon.PlayerLasso.Systems;
using Robust.Client.Physics;
using Robust.Client.Player;

namespace Content.Client._Horizon.PlayerLasso;

/// <summary>
/// Enables client-side physics prediction for the other end of a lasso rope tied to the local player.
/// </summary>
/// <remarks>
/// The client physics island starts from the predicted local player and follows joints, so the rope pulls
/// the entity on the other end into the local simulation even though that entity is not predicted.
/// The client moves it, then the next server state snaps it back, so both players jitter.
/// Predicting the other end keeps the simulation consistent, the same way pulling predicts the pulled entity.
/// </remarks>
public sealed class PlayerLassoPredictionSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly PlayerLassoSystem _lasso = default!;
    [Dependency] private readonly Robust.Client.Physics.PhysicsSystem _physics = default!;

    private HashSet<EntityUid> _predicted = new();
    private HashSet<EntityUid> _next = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UpdateIsPredictedEvent>(OnUpdateIsPredicted);
    }

    private void OnUpdateIsPredicted(ref UpdateIsPredictedEvent args)
    {
        if (_predicted.Contains(args.Uid))
            args.IsPredicted = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _next.Clear();

        if (_player.LocalEntity is { } local)
            CollectRopePartners(local);

        foreach (var uid in _predicted)
        {
            if (!_next.Contains(uid))
                _physics.UpdateIsPredicted(uid);
        }

        foreach (var uid in _next)
        {
            if (!_predicted.Contains(uid))
                _physics.UpdateIsPredicted(uid);
        }

        (_predicted, _next) = (_next, _predicted);
    }

    private void CollectRopePartners(EntityUid local)
    {
        // The local player is tied by someone else's lasso: predict whoever holds the item.
        if (TryComp<LassoedComponent>(local, out var lassoed) &&
            TryComp<PlayerLassoComponent>(lassoed.LassoItem, out var victimLasso) &&
            victimLasso.Anchor == local)
        {
            var itemEnd = _lasso.GetItemEnd(lassoed.LassoItem);
            if (itemEnd != local)
                _next.Add(itemEnd);
        }

        // The local player holds a lasso: predict whatever it is tied to.
        var query = EntityQueryEnumerator<PlayerLassoComponent>();
        while (query.MoveNext(out var item, out var lasso))
        {
            if (lasso.Anchor is not { } anchor || anchor == local)
                continue;

            if (_lasso.GetItemEnd(item) == local)
                _next.Add(anchor);
        }
    }
}
