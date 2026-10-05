using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Goobstation.SlotMachine;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SlotMachineComponent : Component
{
    [DataField, AutoNetworkedField]
    public int SpinCost = 250;

    [ViewVariables]
    public EntProtoId? EmagSpawnEntity;

    [DataField(required: true)]
    public List<ProtoId<PrizePrototype>> Prizes = new();

    [DataField]
    public SoundSpecifier SpinSound = new SoundPathSpecifier("/Audio/_Goobstation/Machines/SlotMachine/slotmachine_spin.ogg");

    [DataField, AutoNetworkedField]
    public float DoAfterTime = 3.8f;

    [DataField, AutoNetworkedField]
    public bool IsSpinning;

    /// <summary>
    /// Id of the item slot that holds the money.
    /// </summary>
    [DataField]
    public string MoneySlot = "money";
}

[Serializable, NetSerializable]
public enum SlotMachineVisuals : byte
{
    Spinning,
}
