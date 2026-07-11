using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.AdvancedRCD;

/// <summary>
/// Defines a buildable structure for the Advanced RCD with material costs.
/// Similar to RCDPrototype but uses materials instead of charges.
/// </summary>
[Prototype("advancedRcd")]
public sealed partial class AdvancedRCDPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Localized name displayed in the menu.
    /// </summary>
    [DataField]
    public LocId Name { get; private set; } = "Unknown";

    /// <summary>
    /// Category for grouping in the radial menu.
    /// </summary>
    [DataField]
    public string Category { get; private set; } = "Main";

    /// <summary>
    /// Icon displayed in the radial menu. If not set, uses the entity's sprite.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Sprite { get; private set; }

    /// <summary>
    /// The entity prototype to spawn.
    /// </summary>
    [DataField]
    public EntProtoId? Prototype { get; private set; }

    /// <summary>
    /// Material costs for building this structure.
    /// Key is material prototype ID, value is amount required.
    /// </summary>
    [DataField]
    public Dictionary<string, int> MaterialCost { get; private set; } = new();

    /// <summary>
    /// Time to build in seconds.
    /// </summary>
    [DataField]
    public float Delay { get; private set; } = 2f;

    /// <summary>
    /// Whether this is a deconstruct operation.
    /// </summary>
    [DataField]
    public bool IsDeconstruct { get; private set; } = false;

    /// <summary>
    /// Sort priority within category. Lower values appear first.
    /// </summary>
    [DataField]
    public int Priority { get; private set; } = 0;
}
