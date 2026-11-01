using Content.Shared._NF.Bank.Components;
using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

[RegisterComponent, NetworkedComponent]
public sealed partial class ScrapProcessorComponent : Component
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public bool DestroyRejected = true;

    [DataField]
    public float PayoutMultiplier = 1f;

    [DataField]
    public SectorBankAccount Account = SectorBankAccount.Frontier;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Gibs whatever living thing with a body touches the machine, paying nothing.
    /// </summary>
    [DataField]
    public bool GibLiving;

    /// <summary>
    /// Damage dealt to living things that touch the machine, once per <see cref="BurnInterval"/>, like the burn of a
    /// shuttle thruster.
    /// </summary>
    [DataField]
    public DamageSpecifier? BurnDamage;

    /// <summary>
    /// Played at whoever is burned, so they know where the pain comes from.
    /// </summary>
    [DataField]
    public SoundSpecifier? BurnSound;

    [DataField]
    public TimeSpan BurnInterval = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public TimeSpan NextBurn;

    [ViewVariables]
    public HashSet<EntityUid> Burning = new();
}
