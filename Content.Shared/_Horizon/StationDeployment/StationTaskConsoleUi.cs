using Content.Shared._Horizon.StationDeployment.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.StationDeployment;

[Serializable, NetSerializable]
public enum StationTaskConsoleUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public readonly record struct StationOrderUiEntry(string Id, ProtoId<StationOrderPrototype> Order);

[Serializable, NetSerializable]
public sealed class StationTaskConsoleBuiState : BoundUserInterfaceState
{
    public readonly List<StationOrderUiEntry> Orders;

    /// <summary>
    /// A category's development level - one order fulfilled equals one level, uncapped.
    /// </summary>
    public readonly Dictionary<ProtoId<TechDisciplinePrototype>, int> Levels;
    public readonly bool CapsulePresent;
    public readonly bool CapsuleDocked;

    /// <summary>
    /// Credits deducted from the station's bank account each time the capsule is summoned.
    /// </summary>
    public readonly int SummonCost;

    /// <summary>
    /// The server time at which the capsule can next be summoned - compare against the client's own
    /// IGameTiming.CurTime to show a live countdown.
    /// </summary>
    public readonly TimeSpan NextSummonTime;

    public StationTaskConsoleBuiState(
        List<StationOrderUiEntry> orders,
        Dictionary<ProtoId<TechDisciplinePrototype>, int> levels,
        bool capsulePresent,
        bool capsuleDocked,
        int summonCost,
        TimeSpan nextSummonTime)
    {
        Orders = orders;
        Levels = levels;
        CapsulePresent = capsulePresent;
        CapsuleDocked = capsuleDocked;
        SummonCost = summonCost;
        NextSummonTime = nextSummonTime;
    }
}

[Serializable, NetSerializable]
public sealed class StationOrderSummonCapsuleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class StationOrderRecallCapsuleMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class StationOrderCancelMessage : BoundUserInterfaceMessage
{
    public readonly string OrderId;

    public StationOrderCancelMessage(string orderId)
    {
        OrderId = orderId;
    }
}
