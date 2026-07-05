using Robust.Shared.GameStates;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponWeight.Components;

/// <summary>
/// Компонент веса оружия.
/// Если вес больше подъёмности скафандра - игрок не может поднять оружие.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AnCoWeaponWeightComponent : Component
{
    /// <summary>
    /// Вес оружия. Чем больше, тем сильнее нужен скафандр для поднятия.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Weight = 1;
}
