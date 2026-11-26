using Content.Shared._Horizon.PlayerLasso.Systems;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.PlayerLasso.Components;

/// <summary>
/// Applied to a player tied to a <see cref="PlayerLassoComponent"/> item by a rope.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(PlayerLassoSystem))]
public sealed partial class LassoedComponent : Component
{
    /// <summary>
    /// The lasso item this player is tied to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid LassoItem;

    /// <summary>
    /// If true, this victim is braced and immune to being reeled in.
    /// Pulling on the rope while braced instead reels the holder towards the victim.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Braced;

    [DataField]
    public ProtoId<AlertPrototype> LassoedAlert = "Lassoed";

    [DataField]
    public ProtoId<AlertPrototype> BracedAlert = "LassoBraced";
}
