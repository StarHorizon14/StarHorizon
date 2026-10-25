using Content.Shared._NF.Bank.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

/// <summary>
/// A pallet that buys everything left on it, like the selling pallets of the cargo sell console, but without a console:
/// loose things (not walls, floor tiles or anything alive) are paid for at their price into a sector bank account
/// and deleted.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ScrapPalletComponent : Component
{
    /// <summary>
    /// What the pallet never takes.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public float PayoutMultiplier = 1f;

    [DataField]
    public SectorBankAccount Account = SectorBankAccount.Frontier;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// How often the pallet looks at what is on it.
    /// </summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextScan;
}
