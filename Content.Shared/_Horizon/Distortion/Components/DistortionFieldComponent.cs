using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Distortion.Components;

/// <summary>
/// Marks an entity as a source of shuttle sensor/console interference.
/// Any shuttle that flies within <see cref="Range"/> gets its consoles glitching out
/// with visual static, scaling up to full-screen noise once the shuttle has flown
/// completely inside the field.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DistortionFieldComponent : Component
{
    /// <summary>
    /// Distance at which shuttle consoles start picking up interference.
    /// Noise intensity scales linearly from 0 at this range up to full strength
    /// once the shuttle has flown all the way into the field.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float Range = 2.5f;

    /// <summary>
    /// If true, the static also shows up on the screens of players aboard the
    /// affected shuttle, not just on its consoles.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool AffectPlayers;
}
