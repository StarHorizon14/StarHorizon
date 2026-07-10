using Robust.Shared.GameStates;

namespace Content.Shared._Horizon._Fractions.AnCo.WeaponWeight.Components;

/// <summary>
/// Компонент подъёмной силы для скафандров.
/// Определяет максимальный вес оружия, которое можно поднять.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AnCoLiftStrengthComponent : Component
{
    /// <summary>
    /// Сила подъёма. Оружие с весом больше этого значения нельзя поднять.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int LiftStrength = 5;
}
