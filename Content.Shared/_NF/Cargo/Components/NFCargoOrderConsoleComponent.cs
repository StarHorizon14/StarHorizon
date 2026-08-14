using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Stacks;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._NF.Cargo.Components;

/// <summary>
/// Handles sending order requests to cargo. Doesn't handle orders themselves via shuttle or telepads.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(SharedNFCargoSystem))]
public sealed partial class NFCargoOrderConsoleComponent : Component
{
    [DataField]
    public SoundSpecifier ErrorSound =
        new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// The stack representing cash dispensed on withdrawals.
    /// </summary>
    [DataField]
    public ProtoId<StackPrototype> CashType = "Credit";

    /// <summary>
    /// All of the <see cref="CargoProductPrototype.Group"/>s that are supported.
    /// </summary>
    [DataField]
    public List<string> AllowedGroups = new() { "market" };

    // Frontier: station taxes
    // Accounts to receive tax value (each currently receives the entirety of the taxed value)
    [DataField]
    public Dictionary<SectorBankAccount, float> TaxAccounts = new();

    /// <summary>
    /// Horizon: If true, this console ignores bank funds entirely. The order can only be paid for
    /// by selling goods placed on the sale pallets linked to this console (see NFCargoPalletConsoleComponent
    /// on the same entity). Whether the excess value is returned to the buyer is controlled by
    /// <see cref="GiveChange"/> - by default it is lost.
    /// </summary>
    [DataField]
    public bool BarterOnly;

    /// <summary>
    /// Horizon: If true, any pallet value left over after paying for a barter order is spawned as
    /// <see cref="CashType"/> and given to the buyer. If false (default), the excess is lost.
    /// </summary>
    [DataField]
    public bool GiveChange;

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
}
