using Content.Shared._NF.Bank.Components;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Horizon.Materials;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(TileReclaimerSystem))]
public sealed partial class TileReclaimerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public bool Broken;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    [DataField, AutoNetworkedField]
    public TimeSpan RecycleDelay = TimeSpan.FromSeconds(0.3f);

    [DataField, AutoNetworkedField]
    public Box2 RecyclingBox = new(-0.5f, 0.5f, 0.5f, 1.5f);

    [DataField, AutoNetworkedField]
    public float Efficiency = 1f;

    [DataField]
    public bool Pay = true;

    [DataField]
    public SectorBankAccount Account = SectorBankAccount.Frontier;

    [DataField, AutoNetworkedField]
    public float SlurpStrength = 1600f;

    /// Horizon: the speed the pull towards the machine never takes a grid above, in metres per second. The pull is an
    [DataField, AutoNetworkedField]
    public float SlurpMaxSpeed = 2f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextRecycle;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;
}
