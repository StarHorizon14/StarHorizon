using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Automender;

/// <summary>
/// Медицинский инструмент на батарейном питании: при использовании на цели периодически
/// наносит небольшое лечение, пока пользователь не остановит, кто-то не сдвинется с места,
/// или не кончится заряд батареи в слоте предмета.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutomenderComponent : Component
{
    /// <summary>
    /// Урон, восстанавливаемый за один тик.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public DamageSpecifier Damage = default!;

    /// <remarks>
    /// Фильтр по типу контейнера урона цели. Если null — работает на всех.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public List<ProtoId<DamageContainerPrototype>>? DamageContainers;

    /// <summary>
    /// Интервал между тиками лечения.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1f);

    /// <summary>
    /// Заряд батареи (Дж), расходуемый за один тик лечения.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ChargeUsePerTick = 20f;

    [DataField]
    public SoundSpecifier? HealingBeginSound;

    /// <summary>
    /// Звук, проигрываемый после каждого успешного тика лечения.
    /// </summary>
    [DataField]
    public SoundSpecifier HealingTickSound = new SoundCollectionSpecifier("sparks");
}
