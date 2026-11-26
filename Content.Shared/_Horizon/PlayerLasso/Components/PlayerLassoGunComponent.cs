using Content.Shared._Horizon.PlayerLasso.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.PlayerLasso.Components;

/// <summary>
/// A <see cref="PlayerLassoComponent"/> that is tied by shooting a rope instead of by hand.
/// The rope end flies out of the gun and ties the rope to the first player it touches.
/// Using the gun in hand drops the rope.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PlayerLassoGunSystem))]
public sealed partial class PlayerLassoGunComponent : Component
{
    /// <summary>
    /// The rope end currently flying out of the gun, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Projectile;
}
