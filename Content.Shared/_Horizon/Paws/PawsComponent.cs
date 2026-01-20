using Content.Shared.Item;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Paws;

/// <summary>
/// Компонент для существ с лапами. По умолчанию блокирует поднятие всех предметов.
/// Используйте WhitelistEntities для разрешения конкретных предметов.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PawsComponent : Component
{
    /// <summary>
    /// Если true, по умолчанию блокируются ВСЕ предметы (кроме тех, что в белом списке).
    /// Если false, работает как раньше - блокируются только предметы с определёнными тегами/компонентами.
    /// По умолчанию: true (блокировать всё)
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BlockAllByDefault = true;

    /// <summary>
    /// Если true, отключает блокировку и позволяет брать все предметы.
    /// По умолчанию: false
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool BypassBlacklist = false;

    /// <summary>
    /// Белый список энтити-прототипов, которые можно взять.
    /// Если BlockAllByDefault = true, можно брать ТОЛЬКО эти предметы.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntProtoId> WhitelistEntities = new();

    /// <summary>
    /// Белый список тегов. Предметы с любым из этих тегов можно взять.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> WhitelistTags = new();

    /// <summary>
    /// Путь к RSI файлу с спрайтами лап для отображения в руках.
    /// Если null, предметы будут отображаться с обычными спрайтами.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? PawsRsiPath = null;

    /// <summary>
    /// Имя состояния спрайта лап. Если не указано, используется "inhand-left" и "inhand-right".
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? PawsStateName = null;

    /// <summary>
    /// Множитель скорости атаки. 0.5 = в 2 раза медленнее.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MeleeAttackRateMultiplier = 0.5f;
}
