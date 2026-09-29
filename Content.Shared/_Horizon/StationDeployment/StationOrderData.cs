using Content.Shared._Horizon.StationDeployment.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.StationDeployment;

/// <summary>
/// A currently active order for a station, mirrors CargoBountyData's shape.
/// </summary>
[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct StationOrderData
{
    [DataField]
    public string Id { get; init; } = string.Empty;

    [DataField(required: true)]
    public ProtoId<StationOrderPrototype> Order { get; init; } = string.Empty;

    public StationOrderData(StationOrderPrototype order, int uniqueIdentifier)
    {
        Order = order.ID;
        Id = $"{order.IdPrefix}{uniqueIdentifier:D3}";
    }
}
