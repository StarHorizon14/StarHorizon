using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon._Fractions.AnCo.Biofabricator;

/// <summary>
/// A memory card that can be bound to a living humanoid by clicking them with it in hand.
/// Only stores the owner's NetUserId (CKey) - their appearance/traits are read fresh from their
/// current character profile at restoration time, not snapshotted at bind time.
/// When the bound player dies and consents, a bound Biofabricator can restore their body from it.
/// </summary>
[RegisterComponent]
public sealed partial class AnCoMemoryCardComponent : Component
{
    [DataField]
    public NetUserId? OwnerUserId;

    [DataField]
    public string? OwnerCharacterName;

    /// <summary>
    /// Implant prototypes captured from the owner's body at the moment they consented to restoration.
    /// </summary>
    [DataField]
    public List<EntProtoId> StoredImplants = new();

    /// <summary>
    /// Set to true once the owner has died and accepted the restoration offer.
    /// Reset back to false after a successful restoration.
    /// </summary>
    [DataField]
    public bool ConsentGranted;
}
