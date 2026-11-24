using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Administration.RtsControl;

/// <summary>
///     Sent by an admin client to order a group of creatures to move to a point (RTS-style).
/// </summary>
[Serializable, NetSerializable]
public sealed class RtsMoveOrderEvent : EntityEventArgs
{
    public readonly List<NetEntity> Units;
    public readonly NetCoordinates Target;

    public RtsMoveOrderEvent(List<NetEntity> units, NetCoordinates target)
    {
        Units = units;
        Target = target;
    }
}
