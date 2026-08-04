using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Horizon.GameRule.Components;

[RegisterComponent, Access(typeof(CastawayRuleSystem))]
public sealed partial class CastawayRuleComponent : Component
{
    [DataField]
    public ProtoId<StartingGearPrototype> StartingGear = "CastawayGear";

    [DataField]
    public int MinDistance = 4000;

    [DataField]
    public int MaxDistance = 6000;

    /// <summary>
    /// Candidate wreck grids spawned near the player; one is picked at random.
    /// </summary>
    [DataField]
    public List<ResPath> WreckGridPaths =
    [
        new("/Maps/Salvage/medium-ruined-emergency-shuttle.yml"),
        new("/Maps/Salvage/small-ship-1.yml"),
        new("/Maps/Salvage/small-syndicate.yml"),
        new("/Maps/Salvage/hauling-shuttle.yml"),
        new("/Maps/Salvage/medium-crashed-shuttle.yml"),
    ];

    /// <summary>
    /// Minimum/maximum distance from the player's spawn point to offset the wreck grid.
    /// </summary>
    [DataField]
    public float WreckMinDistance = 30f;

    [DataField]
    public float WreckMaxDistance = 60f;

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
