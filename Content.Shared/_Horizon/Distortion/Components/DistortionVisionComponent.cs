using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Distortion.Components;

/// <summary>
/// Applied to a mob standing on a shuttle affected by a distortion field whose
/// source has <c>AffectPlayers</c> set. Draws full-screen static on that player's client.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DistortionVisionComponent : Component
{
    /// <summary>
    /// Current noise intensity, from 0 to 1. Mirrors the affected shuttle's <see cref="DistortionAffectedComponent.Intensity"/>.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float Intensity;

    /// <summary>
    /// Mirrors <see cref="DistortionAffectedComponent.OuterIntensity"/> - the faint, edge-of-screen
    /// early-warning ring that appears before <see cref="Intensity"/> does.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float OuterIntensity;
}
