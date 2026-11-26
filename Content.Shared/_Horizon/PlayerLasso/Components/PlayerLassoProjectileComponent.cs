using Content.Shared._Horizon.PlayerLasso.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.PlayerLasso.Components;

/// <summary>
/// The flying end of a rope shot from a <see cref="PlayerLassoGunComponent"/>.
/// Ties the rope to the first player it hits and disappears on anything else or past the rope's length.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(PlayerLassoGunSystem))]
public sealed partial class PlayerLassoProjectileComponent : Component
{
    /// <summary>
    /// The gun this rope end was shot from.
    /// </summary>
    [DataField]
    public EntityUid? Gun;
}
