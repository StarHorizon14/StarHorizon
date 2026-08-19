using Content.Shared.DoAfter;
using Content.Shared.Item;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Storage.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PetStorageComponent : Component
{
    /// <summary>
    /// EntityUid сумки (рюкзака) питомца, в которую он складывает предметы.
    /// Если null, система будет искать ВСЕ рюкзаки во всех слотах инвентаря.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? StorageEntity;

    /// <summary>
    /// Максимальный размер предмета, который можно поднять в лапы.
    /// Null = нет ограничения по размеру.
    /// Примеры: "Tiny", "Small", "Normal", "Large", "Huge", "Ginormous"
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<ItemSizePrototype>? MaxItemSize = "Small";

    /// <summary>
    /// Задержка перед тем как предмет будет положен в сумку (в секундах)
    /// </summary>
    [DataField]
    public float InsertDelay = 5f;

    /// <summary>
    /// Задержка перед тем как предмет будет извлечен из сумки (в секундах)
    /// </summary>
    [DataField]
    public float RemoveDelay = 5f;

    /// <summary>
    /// Прерывать ли действие при движении
    /// </summary>
    [DataField]
    public bool BreakOnMove = true;

    /// <summary>
    /// Прерывать ли действие при получении урона
    /// </summary>
    [DataField]
    public bool BreakOnDamage = true;
}

/// <summary>
/// Событие для DoAfter при складывании предмета в сумку
/// </summary>
[Serializable, NetSerializable]
public sealed partial class PetInsertItemDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// NetEntity ID хранилища, в которое будет помещен предмет
    /// </summary>
    public NetEntity StorageEntity;
}

/// <summary>
/// Событие для DoAfter при извлечении предмета из сумки
/// </summary>
[Serializable, NetSerializable]
public sealed partial class PetRemoveItemDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// NetEntity ID хранилища, из которого будет извлечен предмет
    /// </summary>
    public NetEntity StorageEntity;
}
