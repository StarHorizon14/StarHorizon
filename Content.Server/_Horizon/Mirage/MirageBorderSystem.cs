using Content.Shared._Horizon.Mirage;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Mirage;

/// <summary>
/// Links mirage borders to their anchors and makes sure players near a border
/// receive the entities around its target, so the client can render the mirage.
/// </summary>
public sealed class MirageBorderSystem : SharedMirageBorderSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextUpdate;

    /// <summary>
    /// Mirage targets every session is currently subscribed to by this system.
    /// </summary>
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _subscriptions = new();

    private readonly HashSet<Entity<MirageBorderComponent>> _borders = new();
    private readonly HashSet<EntityUid> _wanted = new();
    private readonly List<EntityUid> _toRemove = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MirageBorderComponent, MapInitEvent>(OnBorderMapInit);
        SubscribeLocalEvent<MirageAnchorComponent, MapInitEvent>(OnAnchorMapInit);
        SubscribeLocalEvent<MirageAnchorComponent, ComponentShutdown>(OnAnchorShutdown);

        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.Disconnected)
            return;

        // The player manager already drops all view subscriptions of a disconnected session.
        _subscriptions.Remove(e.Session);
    }

    private void OnBorderMapInit(Entity<MirageBorderComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Target != null || ent.Comp.TargetId == null)
            return;

        var query = EntityQueryEnumerator<MirageAnchorComponent>();
        while (query.MoveNext(out var uid, out var anchor))
        {
            if (anchor.Id != ent.Comp.TargetId)
                continue;

            SetTarget(ent, uid);
            return;
        }
    }

    private void OnAnchorMapInit(Entity<MirageAnchorComponent> ent, ref MapInitEvent args)
    {
        // The anchor may be initialized after the borders pointing at it, e.g. when it is on another map.
        var query = EntityQueryEnumerator<MirageBorderComponent>();
        while (query.MoveNext(out var uid, out var border))
        {
            if (border.Target == null && border.TargetId == ent.Comp.Id)
                SetTarget((uid, border), ent.Owner);
        }
    }

    private void OnAnchorShutdown(Entity<MirageAnchorComponent> ent, ref ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<MirageBorderComponent>();
        while (query.MoveNext(out var uid, out var border))
        {
            if (border.Target == ent.Owner)
                SetTarget((uid, border), null);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        foreach (var session in _player.Sessions)
        {
            UpdateSession(session);
        }
    }

    private void UpdateSession(ICommonSession session)
    {
        _wanted.Clear();

        if (session.Status == SessionStatus.InGame && session.AttachedEntity is { } attached)
        {
            var coords = _transform.GetMapCoordinates(attached);

            _borders.Clear();
            _lookup.GetEntitiesInRange(coords, MaxViewRange, _borders);

            foreach (var border in _borders)
            {
                if (border.Comp.Target is not { } target || TerminatingOrDeleted(target))
                    continue;

                var borderCoords = _transform.GetMapCoordinates(border);
                if ((borderCoords.Position - coords.Position).LengthSquared() > border.Comp.ViewRange * border.Comp.ViewRange)
                    continue;

                _wanted.Add(target);
            }
        }

        if (!_subscriptions.TryGetValue(session, out var current))
        {
            if (_wanted.Count == 0)
                return;

            current = new HashSet<EntityUid>();
            _subscriptions[session] = current;
        }

        _toRemove.Clear();
        foreach (var target in current)
        {
            if (!_wanted.Contains(target))
                _toRemove.Add(target);
        }

        foreach (var target in _toRemove)
        {
            current.Remove(target);

            if (!TerminatingOrDeleted(target))
                _viewSubscriber.RemoveViewSubscriber(target, session);
        }

        foreach (var target in _wanted)
        {
            if (current.Add(target))
                _viewSubscriber.AddViewSubscriber(target, session);
        }

        if (current.Count == 0)
            _subscriptions.Remove(session);
    }
}
