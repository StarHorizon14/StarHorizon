using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Utility;
using System.Collections.Generic;

namespace Content.Shared._Horizon._Fractions.AnCo.PocketMap;

[RegisterComponent]
public sealed partial class AncoPocketMapComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsOpen = true;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? InnerPortal;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? PocketMap;

    [ViewVariables]
    public ResPath? LoadedMapPath;

    [ViewVariables]
    public HashSet<EntityUid> Occupants = new();

    /// <summary>
    /// Path to the map file loaded for the pocket map.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ResPath MapPath = new("/Maps/_Horizon/Shuttles/pocket-shuttle.yml");

    /// <summary>
    /// Portal prototype spawned on the pocket map.
    /// </summary>
    [DataField]
    public EntProtoId PortalPrototype = "PocketAirlockShuttle";

    /// <summary>
    /// Delay before the user is teleported into the pocket map.
    /// </summary>
    [DataField]
    public TimeSpan EnterDelay = TimeSpan.FromSeconds(3f);

    [DataField]
    public SoundSpecifier EnterSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    /// <summary>
    /// Offset applied to the portal position when placing the entering player.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TeleportOffsetX = 0f;

    /// <summary>
    /// Offset applied to the portal position when placing the entering player.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TeleportOffsetY = 1f;
}
