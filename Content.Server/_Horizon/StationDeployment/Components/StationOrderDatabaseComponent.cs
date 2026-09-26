using Content.Shared._Horizon.StationDeployment;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Horizon.StationDeployment.Components;

/// <summary>
/// Stores all active delivery orders for a deployed station.
/// </summary>
[RegisterComponent]
public sealed partial class StationOrderDatabaseComponent : Component
{
    [DataField]
    public List<StationOrderData> Orders = new();

    /// <summary>
    /// Used to determine unique order IDs.
    /// </summary>
    [DataField]
    public int TotalOrders;

    /// <summary>
    /// The earliest time the cargo capsule can next be summoned.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSummonTime;
}
