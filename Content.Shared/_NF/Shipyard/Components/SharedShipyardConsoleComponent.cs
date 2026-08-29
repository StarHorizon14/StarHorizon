using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Prototypes;
using Content.Shared.Radio;
using Content.Shared.Access;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._NF.Shipyard.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedShipyardSystem)), AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class ShipyardConsoleComponent : Component
{
    public static string TargetIdCardSlotId = "ShipyardConsole-targetId";

    /// <summary>
    /// Fixed container id for the cash slot, mirroring <see cref="TargetIdCardSlotId"/>.
    /// Kept constant (rather than reading <see cref="CashSlotName"/>, which isn't networked)
    /// so the client can reference the slot without waiting on component state.
    /// </summary>
    public static string CashSlotId = "ShipyardConsole-cash";

    [DataField]
    public ItemSlot TargetIdSlot = new();

    /// <summary>
    /// Optional item slot for cash, allowing ships to be paid for with physical currency.
    /// </summary>
    [DataField]
    public ItemSlot? CashSlot = null;

    /// <summary>
    /// Name of the cash slot, if there is one. Null if there isn't.
    /// Should always match <see cref="CashSlotId"/> when set.
    /// </summary>
    [DataField]
    public string? CashSlotName;

    /// <summary>
    /// The type of currency to accept in the cash slot.
    /// </summary>
    [DataField]
    public string? CurrencyStackType;

    /// <summary>
    /// The current balance in the cash slot.
    /// Kept for convenience of access.
    /// </summary>
    [DataField]
    public int CashSlotBalance;

    /// <summary>
    /// If true, ships can only be purchased using physical currency in the cash slot.
    /// The buyer's bank account balance is ignored entirely.
    /// </summary>
    [DataField]
    public bool CashOnly;

    [DataField]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// The comms channel that announces the ship purchase. The purchase is *always* announced
    /// on this channel.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> ShipyardChannel = "Traffic";

    /// <summary>
    /// A second comms channel that announces the ship purchase, with some information redacted.
    /// Currently used for black market and syndicate shipyards to alert the NFSD.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? SecretShipyardChannel = null;

    /// <summary>
    /// If non-empty, specifies the new job title that should be given to the owner of the ship.
    /// </summary>
    [DataField]
    public LocId? NewJobTitle;

    /// <summary>
    /// Access levels to be added to the owner's ID card.
    /// </summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> NewAccessLevels = new();

    /// <summary>
    /// Indicates that the deeds that come from this console can be copied and transferred.
    /// </summary>
    [DataField]
    public bool CanTransferDeed = true;

    /// <summary>
    /// The accounts to receive payment, and the tax rate to apply for ship sales from this console.
    /// </summary>
    [DataField]
    public Dictionary<SectorBankAccount, float> TaxAccounts = new();

    /// <summary>
    /// If true, the base sale rate is ignored before calculating taxes.
    /// </summary>
    [DataField]
    public bool IgnoreBaseSaleRate;

    /// <summary>
    /// The time at which the console will be able to play the deny sound.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextDenySoundTime = TimeSpan.Zero;

    /// <summary>
    /// The minimum time between playing the deny sound.
    /// </summary>
    [DataField]
    public TimeSpan DenySoundDelay = TimeSpan.FromSeconds(2);

    [AutoNetworkedField]
    public NetEntity? CurIdCard;
}
