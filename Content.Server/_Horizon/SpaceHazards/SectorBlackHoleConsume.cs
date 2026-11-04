using Content.Shared.Damage;
using Content.Shared.Damage.Components;
// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Server.StationEvents.Events;
using Content.Shared._Horizon.SpaceHazards;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Horizon.SpaceHazards;

public static class SectorBlackHoleConsume
{
    public const float PullMul = 1.25f;

    private static readonly HashSet<EntityUid> FreeScratch = new(64);

    public static void PullAndConsumeFreeEntities(
        EntityUid holeUid,
        MapId mapId,
        Vector2 worldCenter,
        float pullRadius,
        float horizonRadius,
        float pullAcceleration,
        float dt,
        EntityLookupSystem lookup,
        SharedTransformSystem transform,
        SharedPhysicsSystem physics,
        IEntityManager entMan)
    {
        if (mapId == MapId.Nullspace || pullRadius <= 0f)
            return;

        var consumeR2 = horizonRadius * horizonRadius;
        var pullR2 = pullRadius * pullRadius;

        FreeScratch.Clear();
        lookup.GetEntitiesInRange(mapId, worldCenter, pullRadius, FreeScratch, LookupFlags.Uncontained);

        foreach (var uid in FreeScratch)
        {
            if (uid == holeUid)
                continue;

            if (!CanConsumeFreeEntity(uid, entMan))
                continue;

            var pos = transform.GetWorldPosition(uid);
            var distSq = (worldCenter - pos).LengthSquared();
            if (distSq > pullR2)
                continue;

            if (distSq <= consumeR2)
            {
                // Horizon: things touching the singularity are left alone, not deleted or damaged
                continue;
            }

            if (!entMan.HasComponent<MobStateComponent>(uid))
                continue;

            if (!entMan.TryGetComponent(uid, out PhysicsComponent? body) || body.BodyType == BodyType.Static)
                continue;

            var delta = worldCenter - pos;
            var d = MathF.Sqrt(distSq);
            var dir = delta / d;
            var t = Math.Clamp(1f - d / pullRadius, 0f, 1f);
            var strength = pullAcceleration * PullMul * (0.25f * t + 0.9f * t * t);
            var vel = body.LinearVelocity + dir * strength * dt;

            var outward = Vector2.Dot(vel, -dir);
            if (outward > 0f)
                vel += dir * outward;

            physics.SetLinearVelocity(uid, vel, body: body);
        }
    }

    public static bool CanConsumeFreeEntity(EntityUid uid, IEntityManager entMan)
    {
        if (entMan.Deleted(uid) || entMan.IsQueuedForDeletion(uid))
            return false;

        if (entMan.HasComponent<MapGridComponent>(uid))
            return false;

        if (entMan.HasComponent<GhostComponent>(uid))
            return false;

        if (entMan.HasComponent<SectorCelestialBodyComponent>(uid))
            return false;

        if (entMan.HasComponent<SectorBackgroundPlanetComponent>(uid))
            return false;

        return true;
    }
}
