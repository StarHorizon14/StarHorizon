using Content.Shared._Horizon.SpaceHazards;
using Robust.Shared.Map;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Solar panels close to a sector star produce more power.
/// </summary>
public sealed class StarSolarBoostSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    /// <summary>Power multiplier for a solar panel at <paramref name="panel"/>: 1 unless a star is in range.</summary>
    public float GetBoost(EntityUid panel)
    {
        var xform = Transform(panel);
        if (xform.MapID == MapId.Nullspace)
            return 1f;

        var pos = _transform.GetWorldPosition(xform);
        var boost = 1f;

        var stars = EntityQueryEnumerator<SectorCelestialBodyComponent, TransformComponent>();
        while (stars.MoveNext(out _, out var body, out var starXform))
        {
            if (body.Kind != CelestialKind.Star || starXform.MapID != xform.MapID)
                continue;

            var r = body.SolarBoostRadius;
            if ((_transform.GetWorldPosition(starXform) - pos).LengthSquared() < r * r)
                boost = MathF.Max(boost, body.SolarBoostMultiplier);
        }

        return boost;
    }
}
