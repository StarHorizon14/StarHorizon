using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Horizon.SpaceHazards;
using Content.Shared.Damage;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Sends shuttles that reach a black hole's center on a random FTL jump (within <c>TransitRange</c> of the map
/// origin, i.e. Frontier) through a dedicated transit map. The transit map has its own parallax and keeps damaging
/// the shuttle hull while the jump lasts.
/// </summary>
public sealed class SingularityTransitSystem : EntitySystem
{
    private const int DestinationAttempts = 32;
    private const float DestinationClearance = 400f;
    private const float BodyClearance = 600f;
    private const float StartupSeconds = 1f;

    // The jump lasts ~25 s, so Danger FTL deals only a fraction of the star's damage per tick.
    private const float HullDamageScale = 0.25f;
    private const float MobDamageScale = 0.5f;
    private const int TransitHits = 5;
    private const int TransitTileClears = 4;

    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private readonly List<Entity<MapGridComponent>> _gridScratch = new();
    private readonly HashSet<Entity<MobStateComponent>> _mobScratch = new();

    /// <summary>
    /// Starts the singularity jump for <paramref name="gridUid"/>. Returns false (nothing happened) when the grid is
    /// not a shuttle, is already in FTL, or no free destination was found.
    /// </summary>
    public bool TrySendThroughSingularity(EntityUid gridUid, EntityUid holeUid, SectorCelestialBodyComponent body)
    {
        if (!TryComp<ShuttleComponent>(gridUid, out var shuttle) || HasComp<FTLComponent>(gridUid))
            return false;

        var holeXform = Transform(holeUid);
        if (holeXform.MapUid is not { } mapUid || holeXform.MapID == MapId.Nullspace)
            return false;

        if (!TryPickDestination(holeXform.MapID, body, gridUid, out var destination))
            return false;

        var transitMap = EnsureTransitMap(body);
        var coordinates = new EntityCoordinates(mapUid, destination);

        _shuttle.FTLToCoordinates(
            gridUid,
            shuttle,
            coordinates,
            _random.NextAngle(),
            startupTime: StartupSeconds,
            hyperspaceTime: body.TransitTime,
            transitMap: transitMap);

        var started = HasComp<FTLComponent>(gridUid);
        if (started)
            Log.Info($"{ToPrettyString(gridUid)} was thrown through the singularity to {destination}");

        return started;
    }

    private bool TryPickDestination(MapId mapId, SectorCelestialBodyComponent origin, EntityUid self, out Vector2 destination)
    {
        for (var attempt = 0; attempt < DestinationAttempts; attempt++)
        {
            // Uniform over the disk around the map origin.
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = MathF.Sqrt(_random.NextFloat()) * origin.TransitRange;
            var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (!IsFree(mapId, pos, self))
                continue;

            destination = pos;
            return true;
        }

        destination = default;
        return false;
    }

    private bool IsFree(MapId mapId, Vector2 pos, EntityUid self)
    {
        // Stay away from stars and black holes.
        var bodies = EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (bodies.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var reach = MathF.Max(body.HazardRadius, MathF.Max(body.PullRadius, body.EventHorizonRadius)) + BodyClearance;
            if ((_transform.GetWorldPosition(xform) - pos).LengthSquared() < reach * reach)
                return false;
        }

        // And away from other grids.
        var scratch = _gridScratch;
        scratch.Clear();
        var box = Box2.CenteredAround(pos, new Vector2(DestinationClearance * 2f, DestinationClearance * 2f));
        _mapManager.FindGridsIntersecting(mapId, box, ref scratch);
        foreach (var grid in scratch)
        {
            if (grid.Owner != self)
                return false;
        }

        return true;
    }

    private EntityUid EnsureTransitMap(SectorCelestialBodyComponent body)
    {
        var query = EntityQueryEnumerator<SingularityTransitMapComponent>();
        while (query.MoveNext(out var existing, out _))
        {
            return existing;
        }

        var mapUid = _maps.CreateMap(out _);
        _meta.SetEntityName(mapUid, "Danger FTL");

        // FTLMapComponent makes the shared shuttle logic treat it as hyperspace; EnsureFTLMap skips it because of the marker.
        var ftlMap = EnsureComp<FTLMapComponent>(mapUid);
        ftlMap.Parallax = body.TransitParallax;

        var parallax = EnsureComp<ParallaxComponent>(mapUid);
        parallax.Parallax = body.TransitParallax;
        Dirty(mapUid, parallax);

        var transit = EnsureComp<SingularityTransitMapComponent>(mapUid);
        // Danger FTL hurts like a star, scaled down because the jump is long.
        var star = new SectorCelestialBodyComponent();
        transit.Damage = star.StarDamage * HullDamageScale;
        transit.Hits = TransitHits;
        transit.MobDamage = star.MobHazardDamage * MobDamageScale;
        return mapUid;
    }

    /// <summary>Creatures standing on an intact tile are safe; those left without a floor (or in open space) are hurt.</summary>
    private void DamageMobsOffTile(MapId mapId, Vector2 center, float radius, DamageSpecifier damage)
    {
        if (damage.Empty)
            return;

        _mobScratch.Clear();
        _lookup.GetEntitiesInRange(mapId, center, radius, _mobScratch);

        foreach (var (mob, _) in _mobScratch)
        {
            var xform = Transform(mob);
            if (xform.GridUid is { } gridUid
                && TryComp<MapGridComponent>(gridUid, out var grid)
                && !_maps.GetTileRef(gridUid, grid, xform.Coordinates).Tile.IsEmpty)
            {
                continue;
            }

            _damageable.TryChangeDamage(mob, damage, ignoreResistances: false);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var maps = EntityQueryEnumerator<SingularityTransitMapComponent>();
        while (maps.MoveNext(out var mapUid, out var transit))
        {
            if (now < transit.NextDamage)
                continue;

            transit.NextDamage = now + transit.Interval;
            if (transit.Damage.Empty)
                continue;

            var grids = EntityQueryEnumerator<FTLComponent, MapGridComponent, TransformComponent>();
            while (grids.MoveNext(out var gridUid, out _, out var grid, out var xform))
            {
                if (xform.MapUid != mapUid)
                    continue;

                var aabb = grid.LocalAABB;
                var radius = new Vector2(aabb.Width, aabb.Height).Length() * 0.5f + 4f;
                var center = Vector2.Transform(aabb.Center, _transform.GetWorldMatrix(gridUid));

                SectorCelestialHullDamage.DamageInRadius(
                    gridUid,
                    grid,
                    center,
                    radius,
                    transit.Damage,
                    transit.Hits,
                    _maps,
                    _turf,
                    _damageable,
                    EntityManager,
                    _random);

                // Danger FTL also tears the hull plating apart.
                SectorCelestialHullDamage.ClearBareTilesInRadius(
                    gridUid,
                    grid,
                    center,
                    radius,
                    TransitTileClears,
                    _maps,
                    _turf,
                    EntityManager,
                    _random);

                DamageMobsOffTile(xform.MapID, center, radius, transit.MobDamage);
            }
        }
    }
}
