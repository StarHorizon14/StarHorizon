using Robust.Shared.Prototypes;

namespace Content.Server._Horizon.SpaceHazards;

/// <summary>
/// Worldgen controller: scatters expedition planets (one look per biome) around the sector map.
/// </summary>
[RegisterComponent]
public sealed partial class ExpeditionPlanetPlacerControllerComponent : Component
{
    [DataField]
    public List<string> Biomes = ["Caves", "Grasslands", "Snow", "Lava", "Shadow", "ArdoniumWastes"];

    /// <summary>How many planets to place for every biome.</summary>
    [DataField]
    public int CountPerBiome = 1;

    [DataField]
    public float MinDistance = 1500f;

    [DataField]
    public float MaxDistance = 6500f;

    /// <summary>Minimum distance between two planets, so the 450 unit ranges never overlap.</summary>
    [DataField]
    public float MinSeparation = 1200f;

    /// <summary>
    /// Fixed shader seeds for biomes whose look depends strongly on the seed (e.g. a Lava seed with no lava in view).
    /// Biomes not listed get a random seed.
    /// </summary>
    [DataField]
    public Dictionary<string, float> BiomeSeeds = new()
    {
        ["Lava"] = 8.2f,
        ["Snow"] = 4.1f,
        ["Shadow"] = 7.7f,
    };

    /// <summary>Keep this far from stars, black holes and other planets.</summary>
    [DataField]
    public float BodyClearance = 3000f;

    [DataField]
    public EntProtoId PlanetPrototype = "ExpeditionPlanet";

    [DataField]
    public bool Spawned;
}
