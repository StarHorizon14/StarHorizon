using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Atmos;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Shared.Atmos;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Dataset;
using Content.Shared.Gravity;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Physics;
using Content.Shared.Procedural;
using Content.Shared.Procedural.Loot;
using Content.Shared.Random;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Content.Shared.Salvage.Expeditions.Modifiers;
using Content.Shared._NF.Atmos.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Collections;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Components;

namespace Content.Server._Horizon.Salvage;

/// <summary>
/// Generates a procedural planet + dungeon on its own map, using the same generation pipeline as
/// the salvage expedition console (biome, dungeon, mobs, loot), but as a public FTL destination
/// any shuttle can travel to - not tied to a single station or coordinate disk.
/// </summary>
public sealed class SpawnLostvoidExpeditionJob : Job<bool>
{
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypeManager;
    private readonly AnchorableSystem _anchorable;
    private readonly BiomeSystem _biome;
    private readonly DungeonSystem _dungeon;
    private readonly MetaDataSystem _metaData;
    private readonly SharedMapSystem _map;

    private readonly int _seed;
    private readonly ProtoId<SalvageDifficultyPrototype> _difficulty;

    private readonly ISawmill _sawmill;

    public EntityUid MapUid { get; private set; } = EntityUid.Invalid;
    public Vector2 DungeonLocation { get; private set; }

    private static readonly ProtoId<SalvageDifficultyPrototype> FallbackDifficulty = "NFModerate";
    public static readonly ProtoId<LocalizedDatasetPrototype> PlanetNames = "NamesBorer";

    // Every ore vein type, so the planet is fully mineable regardless of what the rolled biome would normally seed.
    private static readonly string[] OreMarkerLayers =
    {
        "OreIron",
        "OreQuartz",
        "OreCoal",
        "OreSalt",
        "OreGold",
        "OreSilver",
        "OrePlasma",
        "OreUranium",
        "OreDiamond",
        "OreArtifactFragment",
    };

    public SpawnLostvoidExpeditionJob(
        double maxTime,
        IEntityManager entManager,
        ILogManager logManager,
        IPrototypeManager protoManager,
        AnchorableSystem anchorable,
        BiomeSystem biome,
        DungeonSystem dungeon,
        MetaDataSystem metaData,
        SharedMapSystem map,
        int seed,
        ProtoId<SalvageDifficultyPrototype> difficulty,
        CancellationToken cancellation = default) : base(maxTime, cancellation)
    {
        _entManager = entManager;
        _prototypeManager = protoManager;
        _anchorable = anchorable;
        _biome = biome;
        _dungeon = dungeon;
        _metaData = metaData;
        _map = map;
        _seed = seed;
        _difficulty = difficulty;
        _sawmill = logManager.GetSawmill("lostvoid_expedition_job");
    }

    protected override async Task<bool> Process()
    {
        _sawmill.Debug($"Spawning Lostvoid expedition with seed {_seed}");

        var mapUid = _map.CreateMap(out var mapId, runMapInit: false);
        var grid = _entManager.EnsureComponent<MapGridComponent>(mapUid);
        var random = new Random(_seed);

        // Public FTL destination: no coordinate disk required, visible to any shuttle's nav computer.
        var destComp = _entManager.AddComponent<FTLDestinationComponent>(mapUid);
        destComp.RequireCoordinateDisk = false;
        destComp.Enabled = true;

        var destinationName = _entManager.System<SharedSalvageSystem>()
            .GetFTLName(_prototypeManager.Index(PlanetNames), _seed);
        _metaData.SetEntityName(mapUid, destinationName);
        _entManager.AddComponent<FTLBeaconComponent>(mapUid);
        _entManager.EnsureComponent<AtmosDisabledMapComponent>(mapUid);

        if (!_prototypeManager.TryIndex(_difficulty, out var difficultyProto))
            difficultyProto = _prototypeManager.Index(FallbackDifficulty);

        var mission = _entManager.System<SharedSalvageSystem>()
            .GetMission(SalvageMissionType.Elimination, difficultyProto, _seed);

        var missionBiome = _prototypeManager.Index<SalvageBiomeModPrototype>(mission.Biome);

        if (missionBiome.BiomePrototype != null)
        {
            var biome = _entManager.AddComponent<BiomeComponent>(mapUid);
            var biomeSystem = _entManager.System<BiomeSystem>();
            biomeSystem.SetTemplate(mapUid, biome, _prototypeManager.Index<BiomeTemplatePrototype>(missionBiome.BiomePrototype));
            biomeSystem.SetSeed(mapUid, biome, mission.Seed);

            foreach (var oreLayer in OreMarkerLayers)
            {
                biomeSystem.AddMarkerLayer(mapUid, biome, oreLayer);
            }

            var gravity = _entManager.EnsureComponent<GravityComponent>(mapUid);
            gravity.Enabled = true;
            _entManager.Dirty(mapUid, gravity);

            var air = _prototypeManager.Index<SalvageAirMod>(mission.Air);
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            air.Gases.CopyTo(moles, 0);
            var atmos = _entManager.EnsureComponent<MapAtmosphereComponent>(mapUid);
            _entManager.System<AtmosphereSystem>().SetMapSpace(mapUid, air.Space, atmos);
            _entManager.System<AtmosphereSystem>().SetMapGasMixture(mapUid, new GasMixture(moles, mission.Temperature), atmos);

            if (mission.Color != null)
            {
                var lighting = _entManager.EnsureComponent<MapLightComponent>(mapUid);
                lighting.AmbientLightColor = mission.Color.Value;
                _entManager.Dirty(mapUid, lighting);
            }
        }

        _map.InitializeMap(mapId);
        _map.SetPaused(mapUid, true);

        var landingPadRadius = 4;
        var minDungeonOffset = landingPadRadius + 4;
        var dungeonRotation = _dungeon.GetDungeonRotation(_seed);
        var maxDungeonOffset = minDungeonOffset + 12;
        var dungeonOffsetDistance = minDungeonOffset + (maxDungeonOffset - minDungeonOffset) * random.NextFloat();
        var dungeonOffset = new Vector2(0f, dungeonOffsetDistance);
        dungeonOffset = dungeonRotation.RotateVec(dungeonOffset);
        var dungeonMod = _prototypeManager.Index<SalvageDungeonModPrototype>(mission.Dungeon);
        var dungeonConfig = _prototypeManager.Index(dungeonMod.Proto);
        var dungeons = await WaitAsyncTask(_dungeon.GenerateDungeonAsync(dungeonConfig, dungeonMod.Proto, mapUid, grid, (Vector2i)dungeonOffset, _seed));

        var dungeon = dungeons.First();

        if (dungeon.Rooms.Count == 0)
        {
            _entManager.QueueDeleteEntity(mapUid);
            return false;
        }

        DungeonLocation = dungeonOffset;
        MapUid = mapUid;

        var budgetEntries = new List<IBudgetEntry>();

        // Guaranteed loot (e.g. ore layers)
        foreach (var lootProto in _prototypeManager.EnumeratePrototypes<SalvageLootPrototype>())
        {
            if (!lootProto.Guaranteed)
                continue;

            try
            {
                await SpawnDungeonLoot(lootProto, mapUid);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to spawn guaranteed loot {lootProto.ID}: {e}");
            }
        }

        // Mob spawns
        var mobBudget = difficultyProto.MobBudget;
        var faction = _prototypeManager.Index<SalvageFactionPrototype>(mission.Faction);
        var randomSystem = _entManager.System<RandomSystem>();

        foreach (var entry in faction.MobGroups)
        {
            budgetEntries.Add(entry);
        }

        var probSum = budgetEntries.Sum(x => x.Prob);

        while (mobBudget > 0f)
        {
            var entry = randomSystem.GetBudgetEntry(ref mobBudget, ref probSum, budgetEntries, random);
            if (entry == null)
                break;

            try
            {
                await SpawnRandomEntry((mapUid, grid), entry, dungeon, random);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Failed to spawn mobs for {entry.Proto}: {e}");
            }
        }

        // Loot spawns
        var lootTable = difficultyProto.LootTable ?? SharedSalvageSystem.ExpeditionsLootProto;
        var allLoot = _prototypeManager.Index<SalvageLootPrototype>(lootTable);
        var lootBudget = difficultyProto.LootBudget;

        foreach (var rule in allLoot.LootRules)
        {
            switch (rule)
            {
                case RandomSpawnsLoot randomLoot:
                    budgetEntries.Clear();

                    foreach (var entry in randomLoot.Entries)
                    {
                        budgetEntries.Add(entry);
                    }

                    probSum = budgetEntries.Sum(x => x.Prob);

                    while (lootBudget > 0f)
                    {
                        var entry = randomSystem.GetBudgetEntry(ref lootBudget, ref probSum, budgetEntries, random);
                        if (entry == null)
                            break;

                        await SpawnRandomEntry((mapUid, grid), entry, dungeon, random);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        return true;
    }

    private async Task SpawnRandomEntry(Entity<MapGridComponent> grid, IBudgetEntry entry, Dungeon dungeon, Random random)
    {
        await SuspendIfOutOfTime();

        var availableRooms = new ValueList<DungeonRoom>(dungeon.Rooms);
        var availableTiles = new List<Vector2i>();

        while (availableRooms.Count > 0)
        {
            availableTiles.Clear();
            var roomIndex = random.Next(availableRooms.Count);
            var room = availableRooms.RemoveSwap(roomIndex);
            availableTiles.AddRange(room.Tiles);

            while (availableTiles.Count > 0)
            {
                var tile = availableTiles.RemoveSwap(random.Next(availableTiles.Count));

                if (!_anchorable.TileFree(grid, tile, (int)CollisionGroup.MachineLayer,
                        (int)CollisionGroup.MachineLayer))
                {
                    continue;
                }

                var uid = _entManager.SpawnAtPosition(entry.Proto, _map.GridTileToLocal(grid, grid, tile));
                _entManager.RemoveComponent<GhostRoleComponent>(uid);
                _entManager.RemoveComponent<GhostTakeoverAvailableComponent>(uid);
                return;
            }
        }
    }

    private async Task SpawnDungeonLoot(SalvageLootPrototype loot, EntityUid gridUid)
    {
        for (var i = 0; i < loot.LootRules.Count; i++)
        {
            var rule = loot.LootRules[i];

            switch (rule)
            {
                case BiomeMarkerLoot biomeLoot:
                    if (_entManager.TryGetComponent<BiomeComponent>(gridUid, out var biome))
                    {
                        _biome.AddMarkerLayer(gridUid, biome, biomeLoot.Prototype);
                    }
                    break;
                case BiomeTemplateLoot biomeLoot:
                    if (_entManager.TryGetComponent<BiomeComponent>(gridUid, out var biomeTemplate))
                    {
                        _biome.AddTemplate(gridUid, biomeTemplate, "Loot", _prototypeManager.Index<BiomeTemplatePrototype>(biomeLoot.Prototype), i);
                    }
                    break;
            }
        }
    }
}
