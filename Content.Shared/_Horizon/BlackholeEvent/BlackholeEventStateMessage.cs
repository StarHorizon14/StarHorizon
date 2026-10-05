using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.BlackholeEvent;

/// <summary>
/// Server tells the client whether the blackhole lockdown scene should be shown
/// instead of the gameplay state.
/// </summary>
[Serializable, NetSerializable]
public sealed class BlackholeEventStateMessage : EntityEventArgs
{
    public bool Active { get; }

    public BlackholeEventStateMessage(bool active)
    {
        Active = active;
    }
}
