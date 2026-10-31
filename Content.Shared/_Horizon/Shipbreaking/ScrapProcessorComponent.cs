using Content.Shared._NF.Bank.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

/// <summary>
/// A machine that buys loose entities that touch it (stuff coming off a conveyor, thrown or pulled in)
/// for their appraised price and pays the money into a sector bank account, deleting them afterwards.
/// Used for the shipbreaking processor (walls) and barge (furniture), differing only by <see cref="Whitelist"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ScrapProcessorComponent : Component
{
    /// <summary>
    /// What may be processed correctly. Entities that touch the machine but are not on the whitelist
    /// are processed wrongly: destroyed without payment (see <see cref="DestroyRejected"/>).
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Entities the machine never touches at all (e.g. mobs), neither paying nor destroying them.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// Whether entities that fail the whitelist are destroyed for nothing. If false they are left alone.
    /// </summary>
    [DataField]
    public bool DestroyRejected = true;

    /// <summary>
    /// Multiplier applied to the appraised price of the entity.
    /// </summary>
    [DataField]
    public float PayoutMultiplier = 1f;

    /// <summary>
    /// The bank account that receives the money.
    /// </summary>
    [DataField]
    public SectorBankAccount Account = SectorBankAccount.Frontier;

    /// <summary>
    /// Played when something is processed.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound;
}
