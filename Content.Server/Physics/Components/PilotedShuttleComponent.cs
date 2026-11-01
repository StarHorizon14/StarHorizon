// Mono - whole file

namespace Content.Server.Physics.Components;

/// <summary>
///     Stores non-player entities that want to give input to this shuttle, such as autopilots.
/// </summary>
[RegisterComponent]
public sealed partial class PilotedShuttleComponent : Component
{
    /// <summary>
    ///     Sources to query with <see cref="Controllers.GetShuttleInputsEvent"/>.
    ///     Cleaned up automatically if a source did not respond.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> InputSources = new();

    /// <summary>
    ///     Amount of sources, player pilots included, that gave non-zero input last tick.
    /// </summary>
    [ViewVariables]
    public int ActiveSources;
}
