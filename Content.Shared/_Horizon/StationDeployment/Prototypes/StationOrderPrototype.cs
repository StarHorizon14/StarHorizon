using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Horizon.StationDeployment.Prototypes;

/// <summary>
/// A delivery order a deployed station can post: a set of crate contents that, once delivered
/// via a cargo capsule, completes the order and raises the associated development category's level.
/// </summary>
[Prototype]
public sealed partial class StationOrderPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Description = string.Empty;

    /// <summary>
    /// The exact items (by prototype) that must be present (as a whole, across the capsule's
    /// contents) to complete this order.
    /// </summary>
    [DataField(required: true)]
    public List<StationOrderItemEntry> Entries = new();

    /// <summary>
    /// Which development track this order feeds into on completion.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TechDisciplinePrototype> Category;

    [DataField]
    public string IdPrefix = "ORD";

    [DataField]
    public SpriteSpecifier? Sprite;
}

/// <summary>
/// How much of a specific item prototype a station order requires. Unlike cargo bounties, this
/// matches an exact entity prototype rather than a tag/component whitelist, so the console can
/// show the item's own sprite and localized name instead of a manually-written description.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public readonly partial record struct StationOrderItemEntry()
{
    [DataField(required: true)]
    public EntProtoId Item { get; init; } = default!;

    [DataField]
    public int Amount { get; init; } = 1;
}
