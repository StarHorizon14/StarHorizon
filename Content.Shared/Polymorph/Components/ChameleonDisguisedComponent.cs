using Content.Shared.Polymorph.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Physics; // Horizon

namespace Content.Shared.Polymorph.Components;

/// <summary>
/// Added to a player when they use a chameleon projector.
/// Handles making them invisible and revealing when damaged enough or switching hands.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedChameleonProjectorSystem))]
[AutoGenerateComponentState]
public sealed partial class ChameleonDisguisedComponent : Component
{
    /// <summary>
    /// The disguise entity parented to the player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Disguise;

    /// <summary>
    /// For client, whether the user's sprite was previously visible or not.
    /// </summary>
    [DataField]
    public bool WasVisible;

    /// <summary>
    /// Horizon: the player's body type before anchoring, restored on unanchor.
    /// Unanchoring always sets the body to Dynamic otherwise, which makes mobs crawl.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyType? PreviousBodyType;
}
