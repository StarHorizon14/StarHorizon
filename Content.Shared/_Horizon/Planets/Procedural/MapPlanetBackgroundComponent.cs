using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Planets.Procedural;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class MapPlanetBackgroundComponent : Component
{
    [DataField("planets"), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public List<ProceduralPlanetData> Planets = new();
}
