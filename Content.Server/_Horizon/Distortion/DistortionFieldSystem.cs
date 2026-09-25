using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared._Horizon.Distortion.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._Horizon.Distortion;

/// <summary>
/// Scans for shuttles, radar/mass-scanner consoles, and players near <see cref="DistortionFieldComponent"/>
/// sources and glitches them out with static, scaling with proximity - full noise once a shuttle
/// has flown all the way into the field, or once a handheld scanner or player stands right on top of it.
/// Player screens are only affected by fields with <see cref="DistortionFieldComponent.AffectPlayers"/> set.
/// </summary>
public sealed class DistortionFieldSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float ScanInterval = 0.5f;

    // The faint edge-of-screen warning ring reaches 1.5x farther than the main effect.
    private const float OuterRangeMultiplier = 1.5f;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < ScanInterval)
            return;
        _accumulator -= ScanInterval;

        var fieldQuery = EntityQueryEnumerator<DistortionFieldComponent, TransformComponent>();
        var fields = new List<(TransformComponent Xform, DistortionFieldComponent Comp)>();
        while (fieldQuery.MoveNext(out _, out var comp, out var xform))
        {
            fields.Add((xform, comp));
        }

        var affectedEntities = new Dictionary<EntityUid, (float Intensity, float OuterIntensity)>();

        if (fields.Count > 0)
        {
            ScanShuttleGrids(fields, affectedEntities);
            ScanRadarConsoles(fields, affectedEntities);
        }

        ApplyEffects(affectedEntities);
        ScanPlayers(fields);
    }

    // Whole-ship coverage: intensity is based on the field's distance to the shuttle's hull,
    // so the entire ship glitches out once the field is anywhere inside it.
    private void ScanShuttleGrids(
        List<(TransformComponent Xform, DistortionFieldComponent Comp)> fields,
        Dictionary<EntityUid, (float Intensity, float OuterIntensity)> affectedEntities)
    {
        var shuttleQuery = EntityQueryEnumerator<ShuttleComponent, TransformComponent>();
        while (shuttleQuery.MoveNext(out var gridUid, out _, out var gridXform))
        {
            if (gridXform.MapUid == null)
                continue;

            var gridAabb = _physics.GetWorldAABB(gridUid);
            var best = 0f;
            var bestOuter = 0f;

            foreach (var (fieldXform, field) in fields)
            {
                if (fieldXform.MapUid != gridXform.MapUid || field.Range <= 0f)
                    continue;

                var fieldPos = _xform.GetWorldPosition(fieldXform);
                var closest = Vector2.Clamp(fieldPos, gridAabb.BottomLeft, gridAabb.TopRight);
                var distance = (fieldPos - closest).Length();

                var intensity = Math.Clamp(1f - distance / field.Range, 0f, 1f);
                var outerIntensity = Math.Clamp(1f - distance / (field.Range * OuterRangeMultiplier), 0f, 1f);

                if (intensity > best)
                    best = intensity;

                if (outerIntensity > bestOuter)
                    bestOuter = outerIntensity;
            }

            if (best > 0f || bestOuter > 0f)
                MergeBest(affectedEntities, gridUid, best, bestOuter);
        }
    }

    // Per-device coverage: also glitches out standalone radar consoles and handheld mass
    // scanners based on their own exact position, so a mass scanner picks up interference
    // just from being carried near a field, without needing to be aboard a shuttle.
    private void ScanRadarConsoles(
        List<(TransformComponent Xform, DistortionFieldComponent Comp)> fields,
        Dictionary<EntityUid, (float Intensity, float OuterIntensity)> affectedEntities)
    {
        var consoleQuery = EntityQueryEnumerator<RadarConsoleComponent, TransformComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out _, out var consoleXform))
        {
            if (consoleXform.MapUid == null)
                continue;

            var consolePos = _xform.GetWorldPosition(consoleXform);
            var best = 0f;
            var bestOuter = 0f;

            foreach (var (fieldXform, field) in fields)
            {
                if (fieldXform.MapUid != consoleXform.MapUid || field.Range <= 0f)
                    continue;

                var fieldPos = _xform.GetWorldPosition(fieldXform);
                var distance = (fieldPos - consolePos).Length();

                var intensity = Math.Clamp(1f - distance / field.Range, 0f, 1f);
                var outerIntensity = Math.Clamp(1f - distance / (field.Range * OuterRangeMultiplier), 0f, 1f);

                if (intensity > best)
                    best = intensity;

                if (outerIntensity > bestOuter)
                    bestOuter = outerIntensity;
            }

            if (best > 0f || bestOuter > 0f)
                MergeBest(affectedEntities, consoleUid, best, bestOuter);
        }
    }

    private static void MergeBest(
        Dictionary<EntityUid, (float Intensity, float OuterIntensity)> affectedEntities,
        EntityUid uid,
        float intensity,
        float outerIntensity)
    {
        if (affectedEntities.TryGetValue(uid, out var existing))
        {
            affectedEntities[uid] = (
                Math.Max(existing.Intensity, intensity),
                Math.Max(existing.OuterIntensity, outerIntensity));
        }
        else
        {
            affectedEntities[uid] = (intensity, outerIntensity);
        }
    }

    private void ApplyEffects(Dictionary<EntityUid, (float Intensity, float OuterIntensity)> affectedEntities)
    {
        var noLongerAffected = new List<EntityUid>();

        var existing = EntityQueryEnumerator<DistortionAffectedComponent>();
        while (existing.MoveNext(out var uid, out var affected))
        {
            if (affectedEntities.Remove(uid, out var data))
            {
                if (!MathHelper.CloseTo(affected.Intensity, data.Intensity) ||
                    !MathHelper.CloseTo(affected.OuterIntensity, data.OuterIntensity))
                {
                    affected.Intensity = data.Intensity;
                    affected.OuterIntensity = data.OuterIntensity;
                    Dirty(uid, affected);
                }
            }
            else
            {
                noLongerAffected.Add(uid);
            }
        }

        foreach (var uid in noLongerAffected)
        {
            RemComp<DistortionAffectedComponent>(uid);
        }

        foreach (var (uid, data) in affectedEntities)
        {
            var affected = EnsureComp<DistortionAffectedComponent>(uid);
            affected.Intensity = data.Intensity;
            affected.OuterIntensity = data.OuterIntensity;
            Dirty(uid, affected);
        }
    }

    // Per-player coverage: computed straight from each player's own distance to a field
    // (only fields with AffectPlayers set), independent of the whole-grid effect above -
    // otherwise everyone aboard a shuttle would get the same, near-maximum intensity the
    // moment any part of the ship entered a small field, no matter how far into the ship
    // they actually are.
    private void ScanPlayers(List<(TransformComponent Xform, DistortionFieldComponent Comp)> fields)
    {
        var mobQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (mobQuery.MoveNext(out var uid, out _, out var mobXform))
        {
            var best = 0f;
            var bestOuter = 0f;

            if (mobXform.MapUid != null)
            {
                var mobPos = _xform.GetWorldPosition(mobXform);

                foreach (var (fieldXform, field) in fields)
                {
                    if (!field.AffectPlayers || fieldXform.MapUid != mobXform.MapUid || field.Range <= 0f)
                        continue;

                    var fieldPos = _xform.GetWorldPosition(fieldXform);
                    var distance = (fieldPos - mobPos).Length();

                    var intensity = Math.Clamp(1f - distance / field.Range, 0f, 1f);
                    var outerIntensity = Math.Clamp(1f - distance / (field.Range * OuterRangeMultiplier), 0f, 1f);

                    if (intensity > best)
                        best = intensity;

                    if (outerIntensity > bestOuter)
                        bestOuter = outerIntensity;
                }
            }

            if (best > 0f || bestOuter > 0f)
            {
                var vision = EnsureComp<DistortionVisionComponent>(uid);
                if (!MathHelper.CloseTo(vision.Intensity, best) || !MathHelper.CloseTo(vision.OuterIntensity, bestOuter))
                {
                    vision.Intensity = best;
                    vision.OuterIntensity = bestOuter;
                    Dirty(uid, vision);
                }
            }
            else if (HasComp<DistortionVisionComponent>(uid))
            {
                RemComp<DistortionVisionComponent>(uid);
            }
        }
    }
}
