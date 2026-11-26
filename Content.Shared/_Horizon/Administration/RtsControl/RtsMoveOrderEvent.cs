using Robust.Shared.GameObjects;
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

    /// <summary>
    ///     If set, the units are ordered to attack this creature instead of just moving to <see cref="Target"/>.
    /// </summary>
    public readonly NetEntity? AttackTarget;

    public RtsMoveOrderEvent(List<NetEntity> units, NetCoordinates target, NetEntity? attackTarget = null)
    {
        Units = units;
        Target = target;
        AttackTarget = attackTarget;
    }
}
