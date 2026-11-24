using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shuttles.Events;

/// <summary>
/// Sent by the shuttle console UI to land the shuttle on the landing pad under it, or to take off from it.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShuttleConsoleLandingRequest : BoundUserInterfaceMessage
{
    /// <summary>
    /// True to land, false to take off.
    /// </summary>
    public bool Land;
}
