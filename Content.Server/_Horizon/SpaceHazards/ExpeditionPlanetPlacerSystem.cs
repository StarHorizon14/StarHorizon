using System.Numerics;
using Content.Shared._Horizon.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.SpaceHazards;

public sealed class ExpeditionPlanetPlacerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SectorBodyLayoutSystem _layout = default!;
    [Dependency] private readonly ExpeditionPlanetSystem _expeditionPlanets = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExpeditionPlanetPlacerControllerComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, ExpeditionPlanetPlacerControllerComponent component, ComponentStartup args)
    {
        if (component.Spawned || !TryComp<MapComponent>(uid, out var map))
            return;

        if (!_prototypes.HasIndex<EntityPrototype>(component.PlanetPrototype))
        {
            Log.Error($"Expedition planet prototype '{component.PlanetPrototype}' missing");
            return;
        }

        var placed = new List<Vector2>();
        var spawned = 0;

        foreach (var biome in component.Biomes)
        {
            for (var i = 0; i < component.CountPerBiome; i++)
            {
                if (!TryPickPosition(component, placed, map.MapId, out var pos))
                {
                    Log.Warning($"Could not find a free spot for the {biome} expedition planet");
                    continue;
                }

                placed.Add(pos);
                var ent = Spawn(component.PlanetPrototype, new MapCoordinates(pos, map.MapId));
                if (!TryComp<ExpeditionPlanetComponent>(ent, out var planet))
                {
                    QueueDel(ent);
                    continue;
                }

                var seed = component.BiomeSeeds.TryGetValue(biome, out var fixedSeed)
                    ? fixedSeed
                    : 0.01f + _random.NextFloat() * 9.99f;
                _expeditionPlanets.SetupPlanet(ent, planet, biome, seed);
                spawned++;
            }
        }

        component.Spawned = true;
        Log.Info($"Placed {spawned} expedition planets on {ToPrettyString(uid)}");
    }

    private bool TryPickPosition(ExpeditionPlanetPlacerControllerComponent comp, List<Vector2> placed, MapId mapId, out Vector2 pos)
    {
        var minSq = comp.MinSeparation * comp.MinSeparation;

        for (var attempt = 0; attempt < 64; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = _random.NextFloat(comp.MinDistance, comp.MaxDistance);
            pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            var free = _layout.IsFarFromBodies(mapId, pos, MathF.Max(comp.MinSeparation, comp.BodyClearance));
            foreach (var other in placed)
            {
                if ((other - pos).LengthSquared() < minSq)
                {
                    free = false;
                    break;
                }
            }

            if (free)
                return true;
        }

        pos = default;
        return false;
    }
}
