using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.TheEndEvent;

/// <summary>
/// Server periodically sends the current static-noise intensity (0..1) to all clients
/// while the end event is running.
/// </summary>
[Serializable, NetSerializable]
public sealed class TheEndEventProgressMessage : EntityEventArgs
{
    public float Intensity { get; }

    public TheEndEventProgressMessage(float intensity)
    {
        Intensity = intensity;
    }
}

/// <summary>
/// Server tells the client the static has fully covered the screen; the client should
/// switch to the fake connecting scene.
/// </summary>
[Serializable, NetSerializable]
public sealed class TheEndEventReachedMessage : EntityEventArgs
{
}
