using Content.Shared.Cargo.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
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
    /// The items that must be present (as a whole, across the capsule's contents) to complete this order.
    /// Reused directly from the cargo bounty system - a generic whitelist/blacklist + amount entry.
    /// </summary>
    [DataField(required: true)]
    public List<CargoBountyItemEntry> Entries = new();

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
