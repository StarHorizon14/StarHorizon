using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shuttles.Events;

/// <summary>Sent by the shuttle console MAP tab to start or cancel FTL concentration.</summary>
[Serializable, NetSerializable]
public sealed class FtlConcentrationRequest : BoundUserInterfaceMessage
{
    public bool Enable;

    public FtlConcentrationRequest(bool enable)
    {
        Enable = enable;
    }
}
