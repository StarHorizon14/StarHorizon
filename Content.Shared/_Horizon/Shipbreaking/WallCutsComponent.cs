using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WallCutsComponent : Component
{
    [DataField, AutoNetworkedField]
    public CutEdges Cuts;
}
