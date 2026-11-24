using System.Numerics;
using Content.Server._Horizon.Shuttles.Systems;

namespace Content.Server._Horizon.Shuttles.Components;

/// <summary>
/// Added to a shuttle that has landed on the landing pad tiles of another grid.
/// While it is here the shuttle is welded to that grid and cannot be piloted or sent to FTL.
/// </summary>
[RegisterComponent, Access(typeof(ShuttleLandingSystem))]
public sealed partial class LandedShuttleComponent : Component
{
    /// <summary>
    /// The grid the shuttle has landed on.
    /// </summary>
    [ViewVariables]
    public EntityUid Host;

    /// <summary>
    /// Id of the weld joint that holds the shuttle on <see cref="Host"/>.
    /// </summary>
    [ViewVariables]
    public string JointId = string.Empty;

    /// <summary>
    /// Position of the shuttle in the local space of <see cref="Host"/>, taken at landing.
    /// Used to put the shuttle back when the host jumps to another map.
    /// </summary>
    [ViewVariables]
    public Vector2 LocalPosition;

    /// <summary>
    /// Rotation of the shuttle relative to <see cref="Host"/>, taken at landing.
    /// </summary>
    [ViewVariables]
    public Angle LocalRotation;

    /// <summary>
    /// When the shuttle finishes taking off and is released. Null while it just sits on the pad.
    /// </summary>
    [ViewVariables]
    public TimeSpan? TakeOffAt;
}
