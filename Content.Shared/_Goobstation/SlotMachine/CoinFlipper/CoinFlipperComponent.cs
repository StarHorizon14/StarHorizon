using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Goobstation.SlotMachine.CoinFlipper;

/// <summary>
/// This is used for the coinflipper machine.
/// Takes all the money in the slot and either doubles it or keeps it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CoinFlipperComponent : Component
{
    [DataField]
    public SoundSpecifier SpinSound = new SoundPathSpecifier("/Audio/_Goobstation/Machines/SlotMachine/slotmachine_spin.ogg");

    [DataField]
    public SoundSpecifier LoseSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    [DataField]
    public SoundSpecifier WinSound = new SoundPathSpecifier("/Audio/Effects/Arcade/win.ogg");

    [DataField, AutoNetworkedField]
    public float DoAfterTime = 3.8f;

    [DataField, AutoNetworkedField]
    public bool IsSpinning;

    /// <summary>
    /// How much money was put in for the current flip.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PrizeAmount;

    /// <summary>
    /// Chance to double the money.
    /// </summary>
    [DataField]
    public float WinChance = 0.5f;

    /// <summary>
    /// Entity spawned as the prize, its stack count is set to the won amount.
    /// </summary>
    [DataField]
    public EntProtoId CashPrototype = "SpaceCash";

    /// <summary>
    /// Id of the item slot that holds the money.
    /// </summary>
    [DataField]
    public string MoneySlot = "money";
}
