using Content.Shared._NF.Bank.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

[RegisterComponent, NetworkedComponent]
public sealed partial class ScrapPalletComponent : Component
{
    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public float PayoutMultiplier = 1f;

    [DataField]
    public SectorBankAccount Account = SectorBankAccount.Frontier;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextScan;
}
