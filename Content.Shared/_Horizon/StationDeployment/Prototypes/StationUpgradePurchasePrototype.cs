using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Horizon.StationDeployment.Prototypes;

/// <summary>
/// A piece of equipment a station's control console can purchase once the station's development
/// reaches the required category level. Delivered to a CargoPalletBuy pallet on the station's own
/// grid, and only becomes functional once activated there with the station owner's ID card.
/// </summary>
[Prototype]
public sealed partial class StationUpgradePurchasePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId Description = string.Empty;

    /// <summary>
    /// The development category (and level within it) required to unlock this purchase.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TechDisciplinePrototype> Category;

    [DataField(required: true)]
    public int RequiredLevel;

    [DataField(required: true)]
    public int Price;

    /// <summary>
    /// The entity prototype spawned on the purchase pallet.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Entity;

    /// <summary>
    /// Maximum number of times a single station can buy this upgrade. Null means unlimited.
    /// </summary>
    [DataField]
    public int? Limit;

    [DataField]
    public SpriteSpecifier? Sprite;
}
