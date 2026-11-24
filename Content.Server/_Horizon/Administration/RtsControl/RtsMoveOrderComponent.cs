using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization.Manager.Attributes;

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
