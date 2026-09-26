using Content.Shared._Horizon.StationDeployment.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.StationDeployment.Components;

/// <summary>
/// Tracks a deployed station's development progress per category, raised by completing
/// station orders. Server-authoritative bookkeeping, consumed only via the task console's BUI state.
/// </summary>
[RegisterComponent]
public sealed partial class StationDevelopmentComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<TechDisciplinePrototype>, int> Progress = new();

    /// <summary>
    /// How many times this station has bought each upgrade, keyed by purchase ID. Enforces
    /// <see cref="StationUpgradePurchasePrototype.Limit"/>.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<StationUpgradePurchasePrototype>, int> Purchases = new();
}
