using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.GhostTrail;

/// <summary>
/// Компонент, создающий эффект "призрачного следа" за движущейся сущностью.
/// При движении за объектом остаются затухающие полупрозрачные копии спрайта.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GhostTrailComponent : Component
{
    /// <summary>
    /// Минимальный интервал между созданием следов (в секундах).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TrailInterval = 0.05f;

    /// <summary>
    /// Время жизни каждого следа (в секундах).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float TrailLifetime = 0.3f;

    /// <summary>
    /// Начальная прозрачность следа (0.0 - 1.0).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float InitialAlpha = 0.6f;

    /// <summary>
    /// Опциональный цвет/оттенок следа.
    /// Null = использовать оригинальный цвет спрайта.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? TrailColor;

    /// <summary>
    /// Максимальное количество одновременных следов.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxTrails = 10;

    /// <summary>
    /// Минимальное расстояние для создания следа.
    /// Предотвращает создание следов при микродвижениях.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinDistance = 0.1f;

    /// <summary>
    /// Включён ли эффект в данный момент.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;
}
