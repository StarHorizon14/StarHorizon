namespace Content.Server._Horizon.StationDeployment.Components;

/// <summary>
/// Marker for a cargo capsule grid summoned by a station's task console.
/// </summary>
[RegisterComponent]
public sealed partial class CargoCapsuleComponent : Component
{
    /// <summary>
    /// The station that summoned this capsule.
    /// </summary>
    [DataField]
    public EntityUid? OwningStation;

    /// <summary>
    /// Set once the capsule's FTL trip completes and it is docked at the station.
    /// </summary>
    [DataField]
    public bool Docked;
}
