using Robust.Shared.Map;

namespace Content.Server._Horizon.Administration.RtsControl;

/// <summary>
///     Marks a creature that is executing an admin RTS move order.
///     While present, the creature's HTN is disabled.
/// </summary>
[RegisterComponent]
public sealed partial class RtsMoveOrderComponent : Component
{
    [DataField]
    public EntityCoordinates Target;

    /// <summary>
    ///     The point the player clicked, used as a fallback when this creature's formation slot is unreachable.
    /// </summary>
    [DataField]
    public EntityCoordinates CenterTarget;

    [DataField]
    public bool FellBack;

    /// <summary>
    ///     When to next check that the creature is actually making progress.
    /// </summary>
    [DataField]
    public TimeSpan NextProgressCheck;

    /// <summary>
    ///     Where the creature was at the previous progress check.
    /// </summary>
    [DataField]
    public EntityCoordinates LastCheckPosition;
}
