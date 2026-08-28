using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared.Trigger.Components.Effects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridCollisionOnTriggerComponent : BaseXOnTriggerComponent
{
    [DataField, AutoNetworkedField]
    public float ThrowStrength = 4f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new()
    {
        DamageDict = { ["Blunt"] = 3 },
    };

    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(2);

    [DataField, AutoNetworkedField]
    public bool IgnoreResistances;

    [DataField, AutoNetworkedField]
    public bool AffectMobs = true;
}
