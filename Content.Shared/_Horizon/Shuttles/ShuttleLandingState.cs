using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shuttles;

/// <summary>
/// Where a shuttle is with landing pads, as shown on its console.
/// </summary>
[Serializable, NetSerializable]
public enum ShuttleLandingState : byte
{
    /// <summary>Free to fly.</summary>
    None,

    /// <summary>Sitting on a landing pad.</summary>
    Landed,

    /// <summary>Spinning up its engines to take off from a landing pad.</summary>
    TakingOff,
}
