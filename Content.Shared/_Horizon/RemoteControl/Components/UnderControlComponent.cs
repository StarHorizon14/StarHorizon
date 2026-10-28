using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.RemoteControl.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]

public sealed partial class UnderControlComponent : Component
{
    [DataField]
    public EntityUid OriginalBody;

    /// <summary>
    ///     If under control a mech
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HostIsRemotePilot = false;

    [DataField, AutoNetworkedField]
    public EntProtoId ReturnToBodyAction = "ReturnToBodyAction";

    [DataField, AutoNetworkedField]
    public EntityUid? ReturnToBodyActionEntity;

    /// <summary>
    ///     Horizon: the link breaks once the controlled entity is further than this from the original body, in tiles.
    /// </summary>
    [DataField]
    public float MaxRange = 100f;

    /// <summary>
    ///     Horizon: interference static starts to show from this distance and grows until <see cref="MaxRange"/>.
    /// </summary>
    [DataField]
    public float NoiseStartRange = 80f;

    /// <summary>
    ///     Horizon: 0 = clean signal, 1 = about to lose the link. Drives the client's static overlay.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public float SignalLoss;
}
