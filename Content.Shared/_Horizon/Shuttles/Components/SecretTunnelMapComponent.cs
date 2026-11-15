using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shuttles.Components;

/// <summary>
/// Put on the map of a hidden base (syndicate outpost, pirate cove). Its FTL destination only works for a shuttle that
/// is next to <see cref="Planet"/>, a random expedition planet picked at runtime, and ships coming back to the sector
/// can only arrive inside that planet's zone. Nothing on the console hints at either rule.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SecretTunnelMapComponent : Component
{
    /// <summary>Which hidden base this is ("syndicate", "pirate"); notes find their tunnel by it.</summary>
    [DataField]
    public string Id = string.Empty;

    /// <summary>The expedition planet the secret tunnel is tied to. Chosen by the server.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Planet;
}
