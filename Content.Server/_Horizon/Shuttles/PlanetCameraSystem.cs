using System.Linq;
using System.Numerics;
using Content.Shared._Horizon.Shuttles;
using Content.Shared.Parallax.Biomes;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.Shuttles;

/// <summary>
/// Lets the shuttle console MAP "planet camera" look at ground away from the player: a per-player anchor entity at
/// the requested spot is added as a view subscriber, so the biome loads there and its entities reach the client.
/// </summary>
public sealed class PlanetCameraSystem : EntitySystem
{
    private const float MaxDistance = 300f;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(6);

    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<ICommonSession, (EntityUid Anchor, TimeSpan LastRequest)> _anchors = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PlanetCameraEvent>(OnRequest);
        _players.PlayerStatusChanged += OnPlayerStatus;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatus;

        foreach (var session in _anchors.Keys.ToList())
            Remove(session);
    }

    private void OnPlayerStatus(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            Remove(args.Session);
    }

    private void OnRequest(PlanetCameraEvent msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (msg.Position is not { } wanted
            || session.AttachedEntity is not { } player
            || !TryComp(player, out TransformComponent? playerXform)
            || playerXform.MapUid is not { } mapUid
            || !HasComp<BiomeComponent>(mapUid))
        {
            Remove(session);
            return;
        }

        // Only a bounded distance from where the player actually is.
        var origin = _transform.GetWorldPosition(playerXform);
        var offset = wanted - origin;
        if (offset.Length() > MaxDistance)
            wanted = origin + Vector2.Normalize(offset) * MaxDistance;

        var coordinates = new MapCoordinates(wanted, playerXform.MapID);

        if (_anchors.TryGetValue(session, out var existing) && !TerminatingOrDeleted(existing.Anchor))
        {
            _transform.SetWorldPosition(existing.Anchor, wanted);
            _anchors[session] = (existing.Anchor, _timing.CurTime);
            return;
        }

        var anchor = Spawn(null, coordinates);
        _viewSubscriber.AddViewSubscriber(anchor, session);
        _anchors[session] = (anchor, _timing.CurTime);
    }

    private void Remove(ICommonSession session)
    {
        if (!_anchors.Remove(session, out var entry))
            return;

        if (!TerminatingOrDeleted(entry.Anchor))
        {
            _viewSubscriber.RemoveViewSubscriber(entry.Anchor, session);
            Del(entry.Anchor);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_anchors.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (session, entry) in _anchors.ToList())
        {
            if (now - entry.LastRequest > Timeout)
                Remove(session);
        }
    }
}
