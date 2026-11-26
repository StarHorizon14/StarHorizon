using Content.Shared._Horizon.PlayerLasso.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.PlayerLasso.Components;

/// <summary>
/// Applied to a non-mob structure/object tied to a <see cref="PlayerLassoComponent"/> item by a rope.
/// Lets a verb appear directly on the structure to untie it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PlayerLassoSystem))]
public sealed partial class LassoAnchoredComponent : Component
{
    /// <summary>
    /// The lasso item this structure is tied to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid LassoItem;
}
