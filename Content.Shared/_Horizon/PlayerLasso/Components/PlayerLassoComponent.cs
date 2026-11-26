using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Horizon.PlayerLasso.Components;

/// <summary>
/// An item that can be used on a player or any physical structure to tie a rope between them
/// and this item, letting the holder (or victim) walk freely within <see cref="MaxDistance"/>
/// before the rope pulls taut. The rope is anchored to this item, not to the holder directly -
/// dropping the item does not cut the rope.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlayerLassoComponent : Component
{
    /// <summary>
    /// The player or structure currently tied to this lasso, if any.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Anchor;

    /// <summary>
    /// Id of the physics joint tying the anchor to this item.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string JointId = string.Empty;

    /// <summary>
    /// Maximum length of the rope, in meters/tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxDistance = 10f;

    /// <summary>
    /// Index of the largest coil sprite (rope-N) shown while the rope is untied.
    /// The sprite shrinks down to rope-0 as the rope pulls taut.
    /// </summary>
    [DataField]
    public int SpriteSteps = 4;

    /// <summary>
    /// How fast the rope reels in or lets out per second while the holder is pulling on it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ReelRate = 2.5f;

    /// <summary>
    /// Whether the rope can be tied by clicking a target next to you. Off for lassos that tie by shooting.
    /// </summary>
    [DataField]
    public bool TieByHand = true;

    /// <summary>
    /// How long it takes to tie the rope to a target.
    /// </summary>
    [DataField]
    public float TieDelay = 2f;

    /// <summary>
    /// How long it takes to untie the rope.
    /// </summary>
    [DataField]
    public float UntieDelay = 2f;

    /// <summary>
    /// Whether whoever is holding this item is currently reeling the rope in or letting it out.
    /// </summary>
    [DataField, AutoNetworkedField]
    public LassoPullState PullState = LassoPullState.None;

    /// <summary>
    /// Set while a shuttle carrying both ends of the rope changes maps through FTL.
    /// The map change clears the joint, so the rope stays tied while this is set and the joint is recreated afterwards.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InTransit;

    /// <summary>
    /// Rope length saved before the FTL map change, restored with the joint afterwards.
    /// </summary>
    [DataField]
    public float TransitLength;

    /// <summary>
    /// Sprite drawn between the victim and this item.
    /// </summary>
    [DataField]
    public SpriteSpecifier RopeSprite = new SpriteSpecifier.Rsi(new ResPath("Objects/Weapons/Guns/Launchers/grappling_gun.rsi"), "rope");

    [DataField]
    public SoundSpecifier? TieSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_start.ogg");

    [DataField]
    public SoundSpecifier? UntieSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/cuff_takeoff_end.ogg");
}

[Serializable, NetSerializable]
public enum LassoPullState : byte
{
    None,

    /// <summary>
    /// Reeling the rope in, pulling the two ends together.
    /// </summary>
    Reeling,

    /// <summary>
    /// Letting the rope out, pushing the two ends apart.
    /// </summary>
    Loosening,
}
