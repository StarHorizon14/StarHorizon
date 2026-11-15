using System.Numerics;
using Content.Server.Worldgen.Components.Debris;
using Content.Server.Worldgen.Systems.Debris;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Shared.Map;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Keeps sector bodies (black holes, stars, planets) apart from each other and keeps worldgen debris away from them.
/// </summary>
public sealed class SectorBodyLayoutSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DebrisFeaturePlacerControllerComponent, PrePlaceDebrisFeatureEvent>(OnPrePlaceDebris);
        SubscribeLocalEvent<SectorCelestialBodyComponent, ComponentInit>(OnBodyInit);
        SubscribeLocalEvent<ExpeditionPlanetComponent, ComponentInit>(OnPlanetInit);
    }

    /// <summary>True when no star, black hole or planet on the map is within <paramref name="minDistance"/> of <paramref name="pos"/>.</summary>
    public bool IsFarFromBodies(MapId mapId, Vector2 pos, float minDistance)
    {
        var minSq = minDistance * minDistance;

        var bodies = EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (bodies.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == mapId && (_transform.GetWorldPosition(xform) - pos).LengthSquared() < minSq)
                return false;
        }

        var planets = EntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent>();
        while (planets.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == mapId && (_transform.GetWorldPosition(xform) - pos).LengthSquared() < minSq)
                return false;
        }

        var background = EntityQueryEnumerator<SectorBackgroundPlanetComponent, TransformComponent>();
        while (background.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == mapId && (_transform.GetWorldPosition(xform) - pos).LengthSquared() < minSq)
                return false;
        }

        return true;
    }

    private void OnPrePlaceDebris(EntityUid uid, DebrisFeaturePlacerControllerComponent component, ref PrePlaceDebrisFeatureEvent args)
    {
        if (args.Handled)
            return;

        var coords = _transform.ToMapCoordinates(args.Coords);
        if (IsInsideDebrisClearance(coords.MapId, coords.Position))
            args.Handled = true;
    }

    private bool IsInsideDebrisClearance(MapId mapId, Vector2 pos)
    {
        var bodies = EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (bodies.MoveNext(out _, out var body, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var r = body.DebrisClearance;
            if ((_transform.GetWorldPosition(xform) - pos).LengthSquared() < r * r)
                return true;
        }

        var planets = EntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent>();
        while (planets.MoveNext(out _, out var planet, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var r = planet.DebrisClearance;
            if ((_transform.GetWorldPosition(xform) - pos).LengthSquared() < r * r)
                return true;
        }

        return false;
    }

    private void OnBodyInit(EntityUid uid, SectorCelestialBodyComponent body, ComponentInit args)
    {
        ClearDebrisAround(uid, body.DebrisClearance);
    }

    private void OnPlanetInit(EntityUid uid, ExpeditionPlanetComponent planet, ComponentInit args)
    {
        ClearDebrisAround(uid, planet.DebrisClearance);
    }

    /// <summary>Removes debris that was already generated inside the clearance radius.</summary>
    private void ClearDebrisAround(EntityUid uid, float radius)
    {
        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        var center = _transform.GetWorldPosition(xform);
        var radiusSq = radius * radius;

        var debris = EntityQueryEnumerator<OwnedDebrisComponent, TransformComponent>();
        while (debris.MoveNext(out var debrisUid, out _, out var debrisXform))
        {
            if (debrisXform.MapID != xform.MapID)
                continue;

            if ((_transform.GetWorldPosition(debrisXform) - center).LengthSquared() < radiusSq)
                QueueDel(debrisUid);
        }
    }
}
