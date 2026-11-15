// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Horizon.SpaceHazards;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.SpaceHazards;

public sealed class SectorCelestialPlacerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SectorPixelPlanetLightSystem _lights = default!;
    [Dependency] private readonly SectorLandmarkAnchorSystem _landmarks = default!;
    [Dependency] private readonly SectorBodyLayoutSystem _layout = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorCelestialPlacerControllerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorCelestialBodyComponent, MapInitEvent>(OnBodyMapInit);
    }

    private void OnStartup(EntityUid uid, SectorCelestialPlacerControllerComponent component, ComponentStartup args)
    { TrySpawn(uid, component); }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!TryComp(mapUid, out SectorCelestialPlacerControllerComponent? placer)) return;
        TrySpawn(mapUid, placer);
    }

    private void TrySpawn(EntityUid mapUid, SectorCelestialPlacerControllerComponent placer)
    {
        if (placer.Spawned) return;
        if (!TryComp<MapComponent>(mapUid, out var map)) return;

        var plan = new List<CelestialKind>();
        if (placer.ForceKind is { } forced)
        {
            plan.Add(forced);
        }
        else
        {
            for (var i = 0; i < placer.BlackHoleCount; i++)
                plan.Add(CelestialKind.BlackHole);
            for (var i = 0; i < placer.StarCount; i++)
                plan.Add(CelestialKind.Star);
        }

        var placed = new List<(Vector2 Pos, CelestialKind Kind)>();
        foreach (var kind in plan)
        {
            if (!TryPickPosition(placer, kind, placed, map.MapId, out var pos))
            {
                Log.Warning($"Could not find a position for a sector {kind}");
                continue;
            }

            if (SpawnBody(mapUid, map.MapId, placer, kind, pos))
                placed.Add((pos, kind));
        }

        placer.Spawned = true;
    }

    private bool SpawnBody(EntityUid mapUid, MapId mapId, SectorCelestialPlacerControllerComponent placer, CelestialKind kind, Vector2 pos)
    {
        var proto = kind == CelestialKind.Star ? placer.StarPrototype : placer.BlackHolePrototype;
        if (!_prototypes.HasIndex<EntityPrototype>(proto))
        {
            Log.Error($"Celestial prototype '{proto}' missing for map {ToPrettyString(mapUid)}");
            return false;
        }

        var ent = Spawn(proto, new MapCoordinates(pos, mapId));
        if (!TryComp<SectorCelestialBodyComponent>(ent, out var body))
        {
            QueueDel(ent);
            return false;
        }

        body.Kind = kind;
        RandomizeShaderParams(ent, body, mapId);
        Dirty(ent, body);
        _lights.ApplyCelestial(ent, body);
        SyncRadiationSource(ent, body);
        _landmarks.LockToMap(ent);
        Log.Info($"Spawned sector {kind} palette={body.Palette} seed={body.Seed:F2} at {pos} on {ToPrettyString(mapUid)}");
        return true;
    }

    private bool TryPickPosition(
        SectorCelestialPlacerControllerComponent placer,
        CelestialKind kind,
        List<(Vector2 Pos, CelestialKind Kind)> placed,
        MapId mapId,
        out Vector2 pos)
    {
        for (var attempt = 0; attempt < 256; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = _random.NextFloat(placer.MinDistance, placer.MaxDistance);
            var candidate = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (IsValid(placer, kind, candidate, placed, mapId))
            {
                pos = candidate;
                return true;
            }
        }

        // Fallback: straight across the origin from the previous body, which is the farthest possible spot.
        if (placed.Count > 0)
        {
            var opposite = -Vector2.Normalize(placed[^1].Pos) * placer.MaxDistance;
            if (IsValid(placer, kind, opposite, placed, mapId))
            {
                pos = opposite;
                return true;
            }
        }

        pos = default;
        return false;
    }

    private bool IsValid(
        SectorCelestialPlacerControllerComponent placer,
        CelestialKind kind,
        Vector2 candidate,
        List<(Vector2 Pos, CelestialKind Kind)> placed,
        MapId mapId)
    {
        foreach (var (otherPos, otherKind) in placed)
        {
            var required = kind == CelestialKind.BlackHole && otherKind == CelestialKind.BlackHole
                ? placer.BlackHoleSeparation
                : placer.BodySeparation;

            if ((otherPos - candidate).LengthSquared() < required * required)
                return false;
        }

        return _layout.IsFarFromBodies(mapId, candidate, placer.Clearance);
    }

    private void OnBodyMapInit(EntityUid uid, SectorCelestialBodyComponent body, MapInitEvent args)
    {
        if (!body.VisualsInitialized)
        {
            var mapId = Transform(uid).MapID;
            RandomizeShaderParams(uid, body, mapId);
        }
        Dirty(uid, body);
        _lights.ApplyCelestial(uid, body);
        SyncRadiationSource(uid, body);
        _landmarks.LockToMap(uid);
    }

    private void SyncRadiationSource(EntityUid uid, SectorCelestialBodyComponent body)
    {
        if (!TryComp(uid, out RadiationSourceComponent? source)) return;
        var peak = SectorCelestialMobDamage.GetDamageAmount(
            body.MobRadiationDamage,
            SectorCelestialMobDamage.RadiationDamageType);
        SectorCelestialMobDamage.SyncRadiationSource(uid, source, body.RadiationRange, peak);
    }

    private void RandomizeShaderParams(EntityUid uid, SectorCelestialBodyComponent body, MapId mapId)
    {
        body.Seed = 0.01f + _random.NextFloat() * 9.99f;
        var mix = uid.Id * 2654435761u ^ (uint) mapId.GetHashCode() ^ (uint) _random.Next();
        body.Palette = (byte) (mix % (uint) SectorBackgroundPlanetPlacerSystem.PaletteCount);
        body.VisualsInitialized = true;
    }
}
