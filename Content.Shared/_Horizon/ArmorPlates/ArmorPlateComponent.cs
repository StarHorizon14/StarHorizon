using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.ArmorPlates;

/// <summary>
/// Бронепластина. Работает только если лежит в контейнере (хранилище, слоте) одежды,
/// надетой в один из разрешённых слотов (комбинезон, РПС/пояс, верхняя одежда).
/// Пластина снижает входящий урон по своим резистам и сама принимает на себя
/// весь поглощённый урон. Когда прочность заканчивается — пластина ломается, как щит.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(ArmorPlateSystem))]
public sealed partial class ArmorPlateComponent : Component
{
    /// <summary>
    /// Резисты пластины. Применяются к урону, который получает носитель.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public DamageModifierSet Modifiers = new();

    /// <summary>
    /// Максимальная прочность пластины.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaxDurability = 100f;

    /// <summary>
    /// Накопленный пластиной урон. Когда достигает <see cref="MaxDurability"/> — пластина ломается.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Damage;

    /// <summary>
    /// Множитель износа: сколько прочности тратится на каждую единицу поглощённого урона.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WearMultiplier = 1f;

    /// <summary>
    /// Доля оставшейся прочности, ниже которой пластина начинает защищать хуже.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DegradeBelow = 0.5f;

    /// <summary>
    /// Какая доля защиты остаётся у пластины на последних единицах прочности.
    /// Между <see cref="DegradeBelow"/> и нулём защита плавно падает до этого значения.
    /// 1 — пластина не деградирует.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WornEfficiency = 0.6f;

    /// <summary>
    /// Доля цены, которая остаётся у полностью изношенной (сломанной) пластины при продаже.
    /// </summary>
    [DataField]
    public float BrokenPriceFraction = 0.2f;

    /// <summary>
    /// Сломана ли пластина. Сломанная пластина не даёт никакой защиты.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>
    /// Префикс состояний спрайта: {prefix}-icon, {prefix}-world, {prefix}-broken.
    /// </summary>
    [DataField]
    public string? SpritePrefix;

    [DataField]
    public SoundSpecifier? BreakSound = new SoundCollectionSpecifier("MetalBreak");

    [DataField]
    public SoundSpecifier? HitSound = new SoundPathSpecifier("/Audio/Weapons/block_metal1.ogg");

    /// <summary>
    /// Множитель скорости ходьбы для того, кто несёт пластину (где угодно в инвентаре, в том числе в рюкзаке).
    /// Множители нескольких пластин перемножаются.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float WalkSpeedModifier = 1f;

    /// <summary>
    /// Множитель скорости бега для того, кто несёт пластину.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SprintSpeedModifier = 1f;

    /// <summary>
    /// Кого пластина сейчас замедляет. Считается локально на сервере и клиенте.
    /// </summary>
    [ViewVariables]
    public EntityUid? Carrier;

    public float Durability => MathF.Max(0f, MaxDurability - Damage);
}

/// <summary>
/// Вешается на моба, который несёт бронепластины. Хранит список пластин для расчёта замедления.
/// </summary>
[RegisterComponent]
[Access(typeof(ArmorPlateSystem))]
public sealed partial class ArmorPlateCarrierComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Plates = new();
}

/// <summary>
/// Необязательный компонент для одежды: позволяет переопределить сколько пластин
/// в ней одновременно работают. Без него в каждой одежде работает одна пластина.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArmorPlateHolderComponent : Component
{
    [DataField]
    public int MaxActivePlates = 1;

    /// <summary>
    /// Если задано — пластины работают только в этих контейнерах одежды.
    /// </summary>
    [DataField]
    public List<string>? Containers;
}

[Serializable, NetSerializable]
public enum ArmorPlateVisuals : byte
{
    Broken,
}
