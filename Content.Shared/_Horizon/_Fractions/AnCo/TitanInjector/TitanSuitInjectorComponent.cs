using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon._Fractions.AnCo.TitanInjector;

/// <summary>
/// Lets a suit hold a reagent cartridge in an item slot and inject a fixed amount
/// of its solution into the wearer via an action, as long as the cartridge has reagents left.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TitanSuitInjectorComponent : Component
{
    /// <summary>
    /// ID of the ItemSlot that holds the reagent cartridge.
    /// </summary>
    [DataField(required: true)]
    public string CartridgeSlotId = string.Empty;

    /// <summary>
    /// Amount injected into the wearer per use.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(5);

    [DataField]
    public EntProtoId InjectAction = "ActionActivateTitanSuitInjector";

    [DataField, AutoNetworkedField]
    public EntityUid? InjectActionEntity;
}

public sealed partial class TitanSuitInjectEvent : InstantActionEvent;
