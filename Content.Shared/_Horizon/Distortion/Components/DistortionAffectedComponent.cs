using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Distortion.Components;

/// <summary>
/// Applied to a shuttle grid by <c>DistortionFieldSystem</c> while it is within
/// range of one or more <see cref="DistortionFieldComponent"/> sources.
/// Consumed client-side to glitch out that shuttle's consoles.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DistortionAffectedComponent : Component
{
    /// <summary>
    /// Current noise intensity, from 0 (out of range) to 1 (shuttle fully inside the field).
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float Intensity;

    /// <summary>
    /// Intensity of a farther-reaching, faint early-warning static ring shown at the very
    /// edges of the screen. Ramps up starting from 1.5x <see cref="DistortionFieldComponent.Range"/>,
    /// well before <see cref="Intensity"/> kicks in.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float OuterIntensity;
}
