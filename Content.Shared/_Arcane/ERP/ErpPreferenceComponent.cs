using Robust.Shared.GameStates;

namespace Content.Shared._Arcane.ERP;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ErpPreferenceComponent : Component
{
    [DataField, AutoNetworkedField]
    public ErpPreference Preference = ErpPreference.Ask;
}
