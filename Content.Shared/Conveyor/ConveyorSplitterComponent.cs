using Robust.Shared.GameStates;

namespace Content.Shared.Conveyor;

/// <summary>
/// Разветвитель конвейеров - автоматически переключает направление Forward/Reverse по таймеру.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConveyorSplitterComponent : Component
{
    /// <summary>
    /// Интервал переключения между направлениями (в секундах).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SwitchInterval = 2f;

    /// <summary>
    /// Доступные опции интервала для выбора через верб.
    /// </summary>
    [DataField]
    public List<float>? IntervalOptions = new() { 0.5f, 1f, 2f, 3f, 5f };

    /// <summary>
    /// Текущий таймер.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Timer;

    /// <summary>
    /// Текущее состояние - true = Forward, false = Reverse.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsForward = true;
}
