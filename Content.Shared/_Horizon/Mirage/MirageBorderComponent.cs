using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Mirage;

/// <summary>
/// Port of the tgstation mirage border.
/// Shows what is around <see cref="Target"/> on top of the area around this entity,
/// letting players see "through" a world border, a map edge or into another map.
/// The mirage is purely visual, nothing inside of it can be interacted with.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MirageBorderComponent : Component
{
    /// <summary>
    /// The entity whose surroundings are shown by the mirage.
    /// Can be on the same map or on a different one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// If <see cref="Target"/> is not set, the server links this border to the
    /// <see cref="MirageAnchorComponent"/> with the same id. Handy for mapping.
    /// </summary>
    [DataField]
    public string? TargetId;

    /// <summary>
    /// Center of the mirage area, relative to this entity (and of the shown area, relative to the target).
    /// Rotates with the entities.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    /// <summary>
    /// Size of the mirage area in tiles.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Size = new(7f, 7f);

    /// <summary>
    /// Players closer than this to the border get the target area sent to them and see the mirage.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ViewRange = 20f;
}
