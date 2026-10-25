using System.Diagnostics.CodeAnalysis;
using Content.Server.Salvage;
using Content.Shared._Horizon.SpaceHazards;
using Content.Shared.Salvage.Expeditions;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Finds the expedition planet a salvage console is next to, and keeps console UIs in sync as ships move.
/// </summary>
public sealed class ExpeditionPlanetSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SalvageSystem _salvage = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;

    private readonly Dictionary<EntityUid, EntityUid?> _lastPlanet = new();
    private TimeSpan _nextCheck;

    /// <summary>
    /// Sets the biome and seed, and gives the planet a generated name (same generator as expedition destinations) for
    /// its beacon on the shuttle console map. The name does not reveal the biome.
    /// </summary>
    public void SetupPlanet(EntityUid uid, ExpeditionPlanetComponent planet, string biome, float seed)
    {
        planet.BiomeId = biome;
        planet.Seed = seed;
        Dirty(uid, planet);

        var nameSeed = (int) (seed * 10000f);
        _meta.SetEntityName(uid, _salvage.GetFTLName(_protos.Index(SalvageSystem.PlanetNames), nameSeed));
    }

    /// <summary>The closest expedition planet whose range covers <paramref name="uid"/> (usually a console), if any.</summary>
    public bool TryGetPlanetNear(EntityUid uid, [NotNullWhen(true)] out Entity<ExpeditionPlanetComponent> planet)
    {
        planet = default;

        if (!TryComp<TransformComponent>(uid, out var xform) || xform.MapID == MapId.Nullspace)
            return false;

        var pos = _transform.GetWorldPosition(xform);
        var bestDistSq = float.MaxValue;

        var query = EntityQueryEnumerator<ExpeditionPlanetComponent, TransformComponent>();
        while (query.MoveNext(out var planetUid, out var comp, out var planetXform))
        {
            if (planetXform.MapID != xform.MapID)
                continue;

            var distSq = (_transform.GetWorldPosition(planetXform) - pos).LengthSquared();
            if (distSq > comp.Range * comp.Range || distSq >= bestDistSq)
                continue;

            bestDistSq = distSq;
            planet = (planetUid, comp);
        }

        return bestDistSq < float.MaxValue;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var consoles = EntityQueryEnumerator<SalvageExpeditionConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (console.Debug)
                continue;

            EntityUid? current = TryGetPlanetNear(uid, out var planet) ? planet.Owner : null;
            if (_lastPlanet.TryGetValue(uid, out var last) && last == current)
                continue;

            _lastPlanet[uid] = current;
            _salvage.RefreshConsole(uid);
        }
    }
}
