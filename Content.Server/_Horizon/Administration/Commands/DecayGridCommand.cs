using System.Numerics;
using Content.Server.Administration;
using Content.Server.Construction.Components;
using Content.Server.Disposal.Tube;
using Content.Server.GameTicking.Rules.VariationPass.Components.ReplacementMarkers;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Administration;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Light.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Tag;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Horizon.Administration.Commands;

/// <summary>
/// Decays a grid into an abandoned-looking wreck: punches a few random craters/gouges through the
/// hull (space in the core, rusting/damaged floor rings around it), rusts walls and thrusters,
/// breaks station maps/computers, randomly blows solar panels, cuts some cables, and scatters junk.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class DecayGridCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IRobustRandom _sharedRandom = default!;

    // Set at the start of each Execute() call: the server's shared random source by default, or a
    // fresh SeededRandom if the caller passed an explicit seed for reproducible results.
    private IRobustRandom _random = default!;

    // Multiplier applied to every chance/count/radius for this run; 1.0 by default.
    private float _intensity = 1f;

    private float Scale(float chance) => Math.Clamp(chance * _intensity, 0f, 1f);

    public string Command => "decaygrid";
    public string Description => "Turns a grid into an abandoned-looking wreck: craters, rust, broken machines, cut cables, scattered junk.";
    public string Help => $"Usage: {Command} [<gridId>] [intensity] [seed] [epicenters]\n" +
                           "  intensity: multiplier on all chances/counts/radii (default 1.0, e.g. 0.5 = light wear, 2.0 = wrecked)\n" +
                           "  seed: integer seed for reproducible results (default: random)\n" +
                           "  epicenters: number of crater epicenters to punch (default: random 2-4; 0 disables craters entirely)";

    // A private, per-run random source so an explicit seed only affects this command's own rolls,
    // never the server's shared IRobustRandom stream used by everything else.
    private sealed class SeededRandom : IRobustRandom
    {
        private System.Random _random;

        public SeededRandom(int seed) => _random = new System.Random(seed);

        public System.Random GetRandom() => _random;
        public void SetSeed(int seed) => _random = new System.Random(seed);
        public float NextFloat() => _random.NextFloat();
        public int Next() => _random.Next();
        public int Next(int maxValue) => _random.Next(maxValue);
        public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
        public double NextDouble() => _random.NextDouble();
        public void NextBytes(byte[] buffer) => _random.NextBytes(buffer);
        public TimeSpan Next(TimeSpan maxTime) => Next(TimeSpan.Zero, maxTime);
        public TimeSpan Next(TimeSpan minTime, TimeSpan maxTime) => minTime + (maxTime - minTime) * _random.NextDouble();
    }

    private const int MinEpicenters = 2;
    private const int MaxEpicenters = 4; // exclusive upper bound for _random.Next
    private const float MinEpicenterRadius = 6f;
    private const float MaxEpicenterRadius = 14f;
    private const float WedgeHalfAngleDegrees = 25f;
    private const float CableCutChance = 0.1f;
    private const float SolarPanelBreakChance = 1f / 3f;
    private const float JunkSpawnChance = 0.25f;
    private const float AmbientDirtyFloorChance = 0.15f;
    private const float DirtDecalSpawnerChance = 0.2f;
    private const float ItemBreakChance = 0.4f;
    private const float LightBulbBreakChance = 0.2f;
    private const float DimSmallLightChance = 0.2f;
    private const float AsteroidMinRadius = 5f;
    private const float AsteroidMaxRadius = 10f;
    private const float AsteroidPenetrationDepth = 2.5f; // how many tiles deep it's allowed to bite into the hull.
    private const float AsteroidRockFraction = 0.5f; // inner half of the radius becomes rock/sand, rest is plating.
    private const float OreSpawnChance = 0.12f;
    private const float FloorTileItemChance = 0.3f;
    private const float PipeBreakChance = 0.6f;
    private const float GrilleBreakChance = 0.4f;
    private const float MinGlassDamage = 10f;
    private const float MaxGlassDamage = 100f;

    private static readonly ProtoId<EntityPrototype>[] OreRocks =
    [
        "WallRock",
        "WallRockCoal",
        "WallRockTin",
        "WallRockQuartz",
        "WallRockSalt",
        "WallRockGold",
        "WallRockSilver",
        "WallRockPlasma",
        "WallRockUranium",
    ];

    private static readonly ProtoId<EntityPrototype>[] JunkSpawners =
    [
        "SalvageSpawnerScrapCommon75",
        "RandomSpawner100",
        "SalvageSpawnerScrapValuable100",
        "SalvageSpawnerEquipment100",
        "SalvageSpawnerTreasure",
        "SalvageSpawnerEquipmentValuable",
    ];

    // Intact prototype ID -> broken/scrap counterpart to randomly swap it for.
    private static readonly Dictionary<string, EntProtoId> BrokenReplacements = new()
    {
        ["Medkit"] = "ScrapMedkit",
        ["SurveillanceCameraConstructed"] = "ScrapCamera",
        ["SurveillanceCameraEngineering"] = "ScrapCamera",
        ["SurveillanceCameraSecurity"] = "ScrapCamera",
        ["SurveillanceCameraScience"] = "ScrapCamera",
        ["SurveillanceCameraSupply"] = "ScrapCamera",
        ["SurveillanceCameraCommand"] = "ScrapCamera",
        ["SurveillanceCameraService"] = "ScrapCamera",
        ["SurveillanceCameraMedical"] = "ScrapCamera",
        ["SurveillanceCameraGeneral"] = "ScrapCamera",
        ["SurveillanceCameraAssembly"] = "ScrapCamera",
        ["CryoPod"] = "CryoPodDestroyed",
        ["NFVehicleJanicart"] = "VehicleJanicartDestroyed",
        ["FenceMetalStraight"] = "FenceMetalBroken",
        ["FenceMetalCorner"] = "FenceMetalBroken",
        ["FenceMetalEnd"] = "FenceMetalBroken",
        ["FenceMetalGate"] = "FenceMetalBroken",
        ["ComputerWallmountFrame"] = "ComputerWallmountBroken",
        ["Floodlight"] = "FloodlightBroken",
        ["PosterContrabandFreeTonto"] = "PosterBroken",
        ["PosterLegitNanotrasenLogo"] = "PosterBroken",
        ["PosterLegitSafetyInternals"] = "PosterBroken",
        ["PosterContrabandSyndicateRecruitment"] = "PosterBroken",
        ["PosterLegitIan"] = "PosterBroken",
        ["DrinkBottleBeer"] = "BrokenBottle",
        ["DrinkBottleWhiskey"] = "BrokenBottle",
        ["DrinkBottleVodka"] = "BrokenBottle",
        ["DrinkBottleWine"] = "BrokenBottle",
        ["DrinkBottleRum"] = "BrokenBottle",
    };

    private static readonly EntProtoId[] MachineScrapReplacements =
    [
        "ScrapGenericBrokenMachine",
        "ScrapGenericBrokenMachineAncient",
    ];

    private enum DecayLevel
    {
        Space,
        Lattice,
        PlatingDamaged,
        Plating,
        Dirty,
    }

    private sealed class Epicenter
    {
        public Vector2 Center;
        public float Radius;
        public bool IsWedge;
        public Angle WedgeDirection;
    }

    private enum AsteroidLevel
    {
        Rock,
        Plating,
    }

    private sealed class AsteroidSpot
    {
        public Vector2 Center;
        public float Radius;
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 4)
        {
            shell.WriteLine(Help);
            return;
        }

        var player = shell.Player;
        EntityUid? gridId;
        var intensity = 1f;
        int? seed = null;
        int? epicenterCount = null;

        if (args.Length == 0)
        {
            if (player?.AttachedEntity is not { Valid: true } playerEntity)
            {
                shell.WriteError("Only a player can run this command without a grid ID.");
                return;
            }

            gridId = _entManager.GetComponent<TransformComponent>(playerEntity).GridUid;
        }
        else
        {
            if (!NetEntity.TryParse(args[0], out var idNet) || !_entManager.TryGetEntity(idNet, out var id))
            {
                shell.WriteError($"{args[0]} is not a valid entity.");
                return;
            }

            gridId = id;
        }

        if (args.Length >= 2)
        {
            if (!float.TryParse(args[1], out intensity) || intensity <= 0f)
            {
                shell.WriteError($"{args[1]} is not a valid intensity (must be a positive number).");
                return;
            }
        }

        if (args.Length >= 3)
        {
            if (!int.TryParse(args[2], out var parsedSeed))
            {
                shell.WriteError($"{args[2]} is not a valid seed (must be an integer).");
                return;
            }

            seed = parsedSeed;
        }

        if (args.Length >= 4)
        {
            if (!int.TryParse(args[3], out var parsedEpicenters) || parsedEpicenters < 0)
            {
                shell.WriteError($"{args[3]} is not a valid epicenter count (must be zero or a positive integer).");
                return;
            }

            epicenterCount = parsedEpicenters;
        }

        _random = seed is { } s ? new SeededRandom(s) : _sharedRandom;
        _intensity = intensity;

        if (gridId is not { } grid || !_entManager.TryGetComponent(grid, out MapGridComponent? gridComp))
        {
            shell.WriteError($"No grid exists with id {gridId}");
            return;
        }

        var mapSystem = _entManager.System<SharedMapSystem>();
        var poweredLight = _entManager.System<PoweredLightSystem>();
        var tagSystem = _entManager.System<TagSystem>();
        var damageableSystem = _entManager.System<DamageableSystem>();
        var bluntDamage = _protoManager.Index<DamageTypePrototype>("Blunt");

        var epicenters = GenerateEpicenters(gridComp, epicenterCount);
        var asteroidSpot = GenerateAsteroidSpot(gridComp);

        // Tiles with a standing wall on them (rusted/reinforced/grille/asteroid rock) are left
        // completely alone by both gradients below — no tile swap, no loose tile item drop.
        var wallTiles = new HashSet<Vector2i>();
        var wallScanEnumerator = _entManager.GetComponent<TransformComponent>(grid).ChildEnumerator;
        while (wallScanEnumerator.MoveNext(out var wallChild))
        {
            if (!_entManager.EntityExists(wallChild))
                continue;

            var wallProtoId = _entManager.GetComponent<MetaDataComponent>(wallChild).EntityPrototype?.ID;
            var isWall = wallProtoId is "Grille" or "WallRock"
                || _entManager.HasComponent<WallReplacementMarkerComponent>(wallChild)
                || _entManager.HasComponent<ReinforcedWallReplacementMarkerComponent>(wallChild);

            if (!isWall)
                continue;

            var wallXform = _entManager.GetComponent<TransformComponent>(wallChild);
            wallTiles.Add(mapSystem.LocalToTile(grid, gridComp, wallXform.Coordinates));
        }

        var tileChanges = new List<(Vector2i GridIndices, Tile Tile)>();
        var spaceTiles = new HashSet<Vector2i>();
        var junkTiles = new List<Vector2i>();
        var decalTiles = new List<Vector2i>();
        var asteroidRockTiles = new HashSet<Vector2i>();
        var pipeBreakTiles = new HashSet<Vector2i>(); // Lattice/PlatingDamaged: exposed, pipes underneath can break.
        var tileItemTiles = new List<Vector2i>(); // Plating/PlatingDamaged: loose floor tile items get scattered here.

        foreach (var tileRef in mapSystem.GetAllTiles(grid, gridComp))
        {
            if (wallTiles.Contains(tileRef.GridIndices))
                continue;

            var asteroidLevel = GetAsteroidLevel(tileRef.GridIndices, asteroidSpot);
            if (asteroidLevel is not null)
            {
                if (asteroidLevel == AsteroidLevel.Rock)
                {
                    tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["FloorAsteroidSand"].TileId)));
                    asteroidRockTiles.Add(tileRef.GridIndices);
                }
                else
                {
                    tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["PlatingAsteroid"].TileId)));
                }

                continue;
            }

            var level = GetDecayLevel(tileRef.GridIndices, epicenters);
            var isSpace = false;

            switch (level)
            {
                case null:
                    // Untouched by any epicenter: still has a small chance to just be grimy from age.
                    if (_random.Prob(Scale(AmbientDirtyFloorChance)))
                        tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["FloorSteelDirty"].TileId)));
                    break;
                case DecayLevel.Space:
                    tileChanges.Add((tileRef.GridIndices, Tile.Empty));
                    spaceTiles.Add(tileRef.GridIndices);
                    isSpace = true;
                    break;
                case DecayLevel.Lattice:
                    tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["Lattice"].TileId)));
                    junkTiles.Add(tileRef.GridIndices);
                    pipeBreakTiles.Add(tileRef.GridIndices);
                    break;
                case DecayLevel.PlatingDamaged:
                    tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["PlatingDamaged"].TileId)));
                    junkTiles.Add(tileRef.GridIndices);
                    pipeBreakTiles.Add(tileRef.GridIndices);
                    tileItemTiles.Add(tileRef.GridIndices);
                    break;
                case DecayLevel.Plating:
                    tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["Plating"].TileId)));
                    junkTiles.Add(tileRef.GridIndices);
                    tileItemTiles.Add(tileRef.GridIndices);
                    break;
                case DecayLevel.Dirty:
                    tileChanges.Add((tileRef.GridIndices, new Tile(_tileDefManager["FloorSteelDirty"].TileId)));
                    break;
            }

            // Dirt decals get scattered across the whole grid, space tiles excluded (nothing to
            // stand a decal on there).
            if (!isSpace && _random.Prob(Scale(DirtDecalSpawnerChance)))
                decalTiles.Add(tileRef.GridIndices);
        }

        // Entity pass: walls/thrusters/station maps/solar panels/computers/cables, plus anything
        // anchored on a tile that's about to become space gets cut away with it.
        var toSpawn = new List<(EntProtoId Proto, EntityCoordinates Coords, Angle Rotation)>();
        var toDelete = new List<EntityUid>();
        var toDamageWindows = new List<EntityUid>();
        var lightsBroken = 0;

        var enumerator = _entManager.GetComponent<TransformComponent>(grid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            if (!_entManager.EntityExists(child))
                continue;

            var childXform = _entManager.GetComponent<TransformComponent>(child);

            if (childXform.Anchored && spaceTiles.Contains(mapSystem.LocalToTile(grid, gridComp, childXform.Coordinates)))
            {
                toDelete.Add(child);
                continue;
            }

            var protoId = _entManager.GetComponent<MetaDataComponent>(child).EntityPrototype?.ID;
            var tile = mapSystem.LocalToTile(grid, gridComp, childXform.Coordinates);
            var onAsteroidRock = asteroidRockTiles.Contains(tile);

            if (onAsteroidRock && (_entManager.HasComponent<WallReplacementMarkerComponent>(child) || _entManager.HasComponent<ReinforcedWallReplacementMarkerComponent>(child)))
            {
                // An asteroid punched through here: any wall standing in the rock zone gets
                // swallowed and replaced by an actual asteroid rock wall instead of just rusting.
                toSpawn.Add(("WallRock", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<WallReplacementMarkerComponent>(child))
            {
                toSpawn.Add(("WallSolidRust", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<ReinforcedWallReplacementMarkerComponent>(child))
            {
                toSpawn.Add(("WallReinforcedRust", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (protoId == "Grille" && _random.Prob(Scale(GrilleBreakChance)))
            {
                toSpawn.Add(("GrilleBroken", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (tagSystem.HasTag(child, "Window") && _entManager.HasComponent<DamageableComponent>(child))
            {
                // Cracked but not shattered: random damage between the two. Applied after the
                // enumeration finishes, since dealing damage here can cross a Destructible
                // threshold and spawn/delete grid children mid-enumeration.
                toDamageWindows.Add(child);
            }
            else if (pipeBreakTiles.Contains(tile) && _entManager.HasComponent<DisposalTubeComponent>(child) && _random.Prob(Scale(PipeBreakChance)))
            {
                // Disposal tubes don't drop debris on breakage in the base game either (just a
                // construction-node swap into DisposalPipeBroken) — mirror that here directly.
                toSpawn.Add(("DisposalPipeBroken", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (pipeBreakTiles.Contains(tile) && tagSystem.HasTag(child, "Pipe") && _random.Prob(Scale(PipeBreakChance)))
            {
                toSpawn.Add(("GasPipeBroken", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<ThrusterComponent>(child) && protoId != "RustedThruster")
            {
                toSpawn.Add(("RustedThruster", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<StationMapComponent>(child))
            {
                toSpawn.Add(("StationMapBroken", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<SolarPanelReplacementMarkerComponent>(child) && _random.Prob(Scale(SolarPanelBreakChance)))
            {
                toSpawn.Add(("SolarPanelBroken", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<ComputerComponent>(child) && protoId != "ComputerBroken")
            {
                toSpawn.Add(("ComputerBroken", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.TryGetComponent(child, out CableComponent? cable) && _random.Prob(Scale(CableCutChance)))
            {
                toSpawn.Add((cable.CableDroppedOnCutPrototype, childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (protoId != null && BrokenReplacements.TryGetValue(protoId, out var brokenProto) && _random.Prob(Scale(ItemBreakChance)))
            {
                toSpawn.Add((brokenProto, childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.HasComponent<MachineComponent>(child) && _random.Prob(Scale(ItemBreakChance)))
            {
                toSpawn.Add((_random.Pick(MachineScrapReplacements), childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (protoId == "PoweredSmallLight" && _random.Prob(Scale(DimSmallLightChance)))
            {
                toSpawn.Add(("PoweredDimSmallLight", childXform.Coordinates, childXform.LocalRotation));
                toDelete.Add(child);
            }
            else if (_entManager.TryGetComponent(child, out PoweredLightComponent? light) && _random.Prob(Scale(LightBulbBreakChance)))
            {
                poweredLight.TryDestroyBulb(child, light);
                lightsBroken++;
            }
        }

        // Scatter junk on damaged (non-space, non-pristine-dirty-floor) tiles.
        var junkSpawned = 0;
        foreach (var tile in junkTiles)
        {
            if (!_random.Prob(Scale(JunkSpawnChance)))
                continue;

            var coords = mapSystem.GridTileToLocal(grid, gridComp, tile);
            _entManager.SpawnEntity(_random.Pick(JunkSpawners), coords);
            junkSpawned++;
        }

        // Scatter a bit of ore in the asteroid's rock core.
        var oreSpawned = 0;
        foreach (var tile in asteroidRockTiles)
        {
            if (!_random.Prob(Scale(OreSpawnChance)))
                continue;

            var coords = mapSystem.GridTileToLocal(grid, gridComp, tile);
            _entManager.SpawnEntity(_random.Pick(OreRocks), coords);
            oreSpawned++;
        }

        // Scatter loose floor tile items on plating/damaged plating, as if pried up or blown loose.
        // Randomly offset within the tile and rotated so they don't look neatly grid-aligned.
        var tileItemsSpawned = 0;
        foreach (var tile in tileItemTiles)
        {
            if (!_random.Prob(Scale(FloorTileItemChance)))
                continue;

            var offset = new Vector2(_random.NextFloat(-0.3f, 0.3f), _random.NextFloat(-0.3f, 0.3f));
            var coords = mapSystem.GridTileToLocal(grid, gridComp, tile).Offset(offset);
            var spawned = _entManager.SpawnEntity("FloorTileItemSteel", coords);
            _entManager.GetComponent<TransformComponent>(spawned).LocalRotation = _random.NextAngle();
            tileItemsSpawned++;
        }

        mapSystem.SetTiles(grid, gridComp, tileChanges);

        foreach (var tile in decalTiles)
        {
            var coords = mapSystem.GridTileToLocal(grid, gridComp, tile);
            _entManager.SpawnEntity("DecalSpawnerDirtWide", coords);
        }

        foreach (var (proto, coords, rotation) in toSpawn)
        {
            var spawned = _entManager.SpawnEntity(proto, coords);
            _entManager.GetComponent<TransformComponent>(spawned).LocalRotation = rotation;
        }

        foreach (var uid in toDelete)
            _entManager.QueueDeleteEntity(uid);

        var windowsDamaged = 0;
        foreach (var uid in toDamageWindows)
        {
            if (!_entManager.TryGetComponent(uid, out DamageableComponent? windowDamage))
                continue;

            var damage = _random.NextFloat(MinGlassDamage, MaxGlassDamage);
            damageableSystem.TryChangeDamage(uid, new DamageSpecifier(bluntDamage, FixedPoint2.New(damage)), ignoreResistances: true, damageable: windowDamage);
            windowsDamaged++;
        }

        shell.WriteLine($"Decayed grid {grid}: {tileChanges.Count} tiles, {toSpawn.Count} entities replaced/cut, {lightsBroken} light bulbs broken, {windowsDamaged} windows cracked, {junkSpawned} junk spawners placed, {decalTiles.Count} dirt decal spawners placed, {oreSpawned} ore rocks placed, {tileItemsSpawned} loose floor tiles placed.");
    }

    private List<Epicenter> GenerateEpicenters(MapGridComponent gridComp, int? epicenterCount)
    {
        if (epicenterCount == 0)
            return [];

        var aabb = gridComp.LocalAABB;
        var count = epicenterCount ?? Math.Max(1, (int)MathF.Round(_random.Next(MinEpicenters, MaxEpicenters + 1) * _intensity));
        var epicenters = new List<Epicenter>(count);

        for (var i = 0; i < count; i++)
        {
            var center = new Vector2(
                _random.NextFloat(aabb.Left, aabb.Right),
                _random.NextFloat(aabb.Bottom, aabb.Top));

            epicenters.Add(new Epicenter
            {
                Center = center,
                Radius = _random.NextFloat(MinEpicenterRadius, MaxEpicenterRadius) * _intensity,
                IsWedge = _random.Prob(0.5f),
                WedgeDirection = _random.NextAngle(),
            });
        }

        return epicenters;
    }

    // Picks a spot along the edge of the grid's bounding box, simulating an asteroid that grazed
    // the hull from outside rather than a hole punched through the middle. The center is pushed
    // outward past the hull by (radius - AsteroidPenetrationDepth), so no matter how big the
    // asteroid's radius rolls, only a shallow bite is actually taken out of the station's interior.
    private AsteroidSpot GenerateAsteroidSpot(MapGridComponent gridComp)
    {
        var aabb = gridComp.LocalAABB;
        var radius = _random.NextFloat(AsteroidMinRadius, AsteroidMaxRadius);
        var outwardOffset = radius - AsteroidPenetrationDepth;

        var center = _random.Next(4) switch
        {
            0 => new Vector2(aabb.Left - outwardOffset, _random.NextFloat(aabb.Bottom, aabb.Top)),
            1 => new Vector2(aabb.Right + outwardOffset, _random.NextFloat(aabb.Bottom, aabb.Top)),
            2 => new Vector2(_random.NextFloat(aabb.Left, aabb.Right), aabb.Bottom - outwardOffset),
            _ => new Vector2(_random.NextFloat(aabb.Left, aabb.Right), aabb.Top + outwardOffset),
        };

        return new AsteroidSpot { Center = center, Radius = radius };
    }

    private AsteroidLevel? GetAsteroidLevel(Vector2i gridIndices, AsteroidSpot spot)
    {
        var tileCenter = new Vector2(gridIndices.X + 0.5f, gridIndices.Y + 0.5f);
        var distance = (tileCenter - spot.Center).Length();

        if (distance > spot.Radius)
            return null;

        return distance / spot.Radius < AsteroidRockFraction ? AsteroidLevel.Rock : AsteroidLevel.Plating;
    }

    private DecayLevel? GetDecayLevel(Vector2i gridIndices, List<Epicenter> epicenters)
    {
        var tileCenter = new Vector2(gridIndices.X + 0.5f, gridIndices.Y + 0.5f);
        DecayLevel? best = null;

        foreach (var epicenter in epicenters)
        {
            var offset = tileCenter - epicenter.Center;
            var distance = offset.Length();

            if (epicenter.IsWedge)
            {
                var angleToTile = new Angle(offset);
                var delta = Math.Abs(Angle.ShortestDistance(epicenter.WedgeDirection, angleToTile).Degrees);
                if (delta > WedgeHalfAngleDegrees)
                    continue;
            }

            if (distance > epicenter.Radius)
                continue;

            var fraction = distance / epicenter.Radius;
            var level = fraction switch
            {
                < 0.2f => DecayLevel.Space,
                < 0.45f => DecayLevel.Lattice,
                < 0.7f => DecayLevel.PlatingDamaged,
                < 0.9f => DecayLevel.Plating,
                _ => DecayLevel.Dirty,
            };

            if (best is null || level < best)
                best = level;
        }

        return best;
    }
}
