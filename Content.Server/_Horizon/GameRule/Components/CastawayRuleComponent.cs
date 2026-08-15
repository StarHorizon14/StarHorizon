using Content.Shared.Procedural;
using Content.Shared.Roles;
using Content.Shared.Salvage.Expeditions;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Horizon.GameRule.Components;

[RegisterComponent, Access(typeof(CastawayRuleSystem))]
public sealed partial class CastawayRuleComponent : Component
{
    [DataField]
    public ProtoId<StartingGearPrototype> StartingGear = "CastawayGear";

    [DataField]
    public int MinDistance = 500;

    [DataField]
    public int MaxDistance = 1000;

    /// <summary>
    /// Starting gear for the derelict pod scenario: no suit/helmet, just what a passenger would wear.
    /// </summary>
    [DataField]
    public ProtoId<StartingGearPrototype> PodStartingGear = "CastawayPodGear";

    /// <summary>
    /// Map loaded for the derelict pod scenario; the player is spawned inside a MedicalPodSpawn
    /// entity hand-placed on this map.
    /// </summary>
    [DataField]
    public ResPath PodMapPath = new("/Maps/_Horizon/Lostvoid/derelict.yml");

    /// <summary>
    /// Name of the hand-placed wreck grid on PodMapPath that becomes the player's ShuttleDeed
    /// property for the derelict pod scenario, matched by its MetaData entity name.
    /// </summary>
    [DataField]
    public string PodWreckGridName = "Разрушеный шаттл";

    /// <summary>
    /// Candidate wreck grids spawned near the player; one is picked at random.
    /// </summary>
    [DataField]
    public List<ResPath> WreckGridPaths =
    [
        new("/Maps/_Horizon/Lostvoid/Shuttle/whiteship-bluespacejumper.yml"),
        new("/Maps/_Horizon/Lostvoid/Shuttle/hauling-shuttle.yml"),
        new("/Maps/_Horizon/Lostvoid/Shuttle/medium-crashed-shuttle.yml"),
        new("/Maps/_Horizon/Lostvoid/Shuttle/small-syndicate.yml"),
        new("/Maps/_Horizon/Lostvoid/Shuttle/small-ship-1.yml"),
        new("/Maps/_Horizon/Lostvoid/Shuttle/medium-ruined-emergency-shuttle.yml"),
    ];

    /// <summary>
    /// Minimum/maximum distance from the player's spawn point to offset the wreck grid.
    /// </summary>
    [DataField]
    public float WreckMinDistance = 30f;

    [DataField]
    public float WreckMaxDistance = 60f;

    /// <summary>
    /// Salvage wreck grids scattered across the whole map once at round start, independent of player spawns.
    /// </summary>
    [DataField]
    public List<ResPath> MapWreckGridPaths =
    [
        new("/Maps/Salvage/small-1.yml"),
        new("/Maps/Salvage/small-2.yml"),
        new("/Maps/Salvage/small-3.yml"),
        new("/Maps/Salvage/small-4.yml"),
        new("/Maps/Salvage/small-a-1.yml"),
        new("/Maps/Salvage/small-cargo.yml"),
        new("/Maps/Salvage/small-chapel.yml"),
        new("/Maps/Salvage/small-chef.yml"),
        new("/Maps/Salvage/small-party.yml"),
        new("/Maps/Salvage/small-ship-1.yml"),
        new("/Maps/Salvage/small-syndicate.yml"),
        new("/Maps/Salvage/medium-1.yml"),
        new("/Maps/Salvage/medium-dock.yml"),
        new("/Maps/Salvage/medium-library.yml"),
        new("/Maps/Salvage/medium-pet-hospital.yml"),
        new("/Maps/Salvage/medium-pirate.yml"),
        new("/Maps/Salvage/medium-ruined-emergency-shuttle.yml"),
        new("/Maps/Salvage/medium-silent-orchestra.yml"),
        new("/Maps/Salvage/medium-vault-1.yml"),
        new("/Maps/Salvage/hauling-shuttle.yml"),
        new("/Maps/Salvage/medium-crashed-shuttle.yml"),
        new("/Maps/Salvage/cargo-1.yml"),
        new("/Maps/Salvage/engineering-chunk.yml"),
        new("/Maps/Salvage/security-chunk.yml"),
    ];

    /// <summary>
    /// How many wreck grids to scatter across the map at round start.
    /// </summary>
    [DataField]
    public int MapWreckCount = 20;

    /// <summary>
    /// Distance range (from map center) in which to scatter round-start wreck grids.
    /// </summary>
    [DataField]
    public float MapWreckMinDistance = 500f;

    [DataField]
    public float MapWreckMaxDistance = 12000f;

    /// <summary>
    /// Minimum distance between two round-start wreck grids to avoid overlaps.
    /// </summary>
    [DataField]
    public float MapWreckClearance = 150f;

    /// <summary>
    /// How many times to retry finding a non-overlapping spot for a wreck before giving up on it.
    /// </summary>
    [DataField]
    public int MapWreckPlacementRetries = 10;

    /// <summary>
    /// Procedurally-generated dungeons (with mobs) scattered across the map at round start, same
    /// generation as the BluespaceErrorRule station event's VGRoid dungeons, but spawned directly
    /// by this rule instead of through the station-event scheduler/announcement pipeline.
    /// </summary>
    [DataField]
    public List<ProtoId<DungeonConfigPrototype>> MapDungeons =
    [
        "NFVGRoidBasalt",
        "NFVGRoidSnow",
        "NFVGRoidCave",
        "NFVGRoidChromite",
    ];

    [DataField]
    public List<ProtoId<SalvageFactionPrototype>> MapDungeonFactions =
    [
        "NFXenos",
        "NFCarps",
        "NFFlesh",
    ];

    /// <summary>
    /// Mob spawn budget for each dungeon, same meaning as DungeonSpawnComponent.MobBudget.
    /// </summary>
    [DataField]
    public int MapDungeonMobBudget = 30;

    /// <summary>
    /// How many dungeons to scatter across the map at round start.
    /// </summary>
    [DataField]
    public int MapDungeonCount = 5;

    /// <summary>
    /// Survival items scattered in space around the player's spawn point.
    /// </summary>
    [DataField]
    public List<EntProtoId> SurvivalLoot =
    [
        "EmergencyOxygenTankFilled",
        "MedkitFilled",
        "FlashlightLantern",
    ];

    /// <summary>
    /// Minimum/maximum distance from the player's spawn point to scatter survival loot.
    /// </summary>
    [DataField]
    public float LootMinDistance = 2f;

    [DataField]
    public float LootMaxDistance = 5f;
}
