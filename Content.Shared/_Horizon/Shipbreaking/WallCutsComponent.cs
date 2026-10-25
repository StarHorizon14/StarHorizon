using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

/// <summary>
/// The edges of a wall that were cut with a <see cref="ShipCutterComponent"/>. A wall comes loose once it is cut off
/// from every wall next to it. Unlike <see cref="GridCutsComponent"/> these cuts let no air through and split nothing.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WallCutsComponent : Component
{
    [DataField, AutoNetworkedField]
    public CutEdges Cuts;
}
