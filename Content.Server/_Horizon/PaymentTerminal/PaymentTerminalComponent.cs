using Content.Shared._Horizon.PaymentTerminal;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Audio;
using Robust.Shared.Network;

namespace Content.Server._Horizon.PaymentTerminal;

/// <summary>
/// A card terminal bound to its owner's bank account.
/// Swiping an ID charges the person swiping and pays the owner, minus a tax to a sector account.
/// </summary>
[RegisterComponent, Access(typeof(PaymentTerminalSystem))]
public sealed partial class PaymentTerminalComponent : Component
{
    /// <summary>
    /// Account of the player whose character receives the payments. Null while unbound.
    /// </summary>
    [ViewVariables]
    public NetUserId? OwnerUserId;

    /// <summary>
    /// Name of the owner's character, used to find the right bank account among their characters.
    /// </summary>
    [ViewVariables]
    public string? OwnerCharacterName;

    /// <summary>
    /// Name of the ID the terminal was bound with, shown on examine and in the UI.
    /// </summary>
    [ViewVariables]
    public string? OwnerIdName;

    [DataField]
    public PaymentTerminalMode Mode = PaymentTerminalMode.Fixed;

    [ViewVariables]
    public PaymentTerminalStage Stage = PaymentTerminalStage.Price;

    /// <summary>
    /// In <see cref="PaymentTerminalMode.Fixed"/> mode, set once the owner confirms a price; it stays until they clear it.
    /// </summary>
    [ViewVariables]
    public bool PriceLocked;

    [ViewVariables]
    public int Price;

    [ViewVariables]
    public int Tip;

    [DataField]
    public int MaxAmount = 1_000_000;

    /// <summary>
    /// Fraction of each payment withheld from the owner and sent to <see cref="TaxAccount"/>.
    /// Shown to the buyer in the total.
    /// </summary>
    [DataField]
    public float TaxRate = 0.1f;

    [DataField]
    public SectorBankAccount TaxAccount = SectorBankAccount.Frontier;

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier KeypadSound = new SoundPathSpecifier("/Audio/Machines/Nuke/general_beep.ogg");
}
