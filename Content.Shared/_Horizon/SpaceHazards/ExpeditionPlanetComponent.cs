using Content.Shared.Salvage.Expeditions;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.SpaceHazards;

/// <summary>
/// A planet in the sector map that expeditions can be started from. Salvage expedition consoles only work within
/// <see cref="Range"/> of one, and the expedition uses this planet's biome.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ExpeditionPlanetComponent : Component
{
    /// <summary>Salvage biome mod id (Caves, Grasslands, Snow, Lava, Shadow...). Also selects the planet's look.</summary>
    [DataField, AutoNetworkedField]
    public string BiomeId = "Grasslands";

    [DataField, AutoNetworkedField]
    public float SpriteRadius = 40f;

    [DataField, AutoNetworkedField]
    public float Seed;

    /// <summary>Resolution of the pixel-art planet (pixels across the diameter).</summary>
    [DataField, AutoNetworkedField]
    public float Pixels = 160f;

    /// <summary>No worldgen debris is placed (and existing debris is removed) within this distance of the planet.</summary>
    [DataField]
    public float DebrisClearance = 2000f;

    /// <summary>Distance from the planet center within which a console can start an expedition.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 450f;

    /// <summary>
    /// The planet's own pool of expedition offers, identical for every console that comes near. Server-side only,
    /// filled lazily and replaced once an offer is claimed.
    /// </summary>
    [ViewVariables]
    public List<SalvageMissionParams> Missions = new();
}
