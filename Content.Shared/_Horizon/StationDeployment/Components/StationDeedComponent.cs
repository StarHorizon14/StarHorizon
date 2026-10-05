using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.StationDeployment.Components;

/// <summary>
/// Tied to an ID card (and mirrored onto the station's grid) when a station is deployed via a
/// <see cref="StationDeploymentKitComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StationDeedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? StationUid;

    [DataField, AutoNetworkedField]
    public EntityUid? StationGridUid;

    [DataField, AutoNetworkedField]
    public string? StationName;

    /// <summary>
    /// Persistent station code (e.g. "NX-042"), parsed out of the randomly generated name on deployment.
    /// Kept stable across renames, like <c>ShuttleDeedComponent.ShuttleNameSuffix</c>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? StationNumber;
}
