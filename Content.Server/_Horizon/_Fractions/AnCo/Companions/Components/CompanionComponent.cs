using Content.Server.NPC.HTN;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon._Fractions.AnCo.Companions.Components;

/// <summary>
/// Описывает сущность компаньона.<br/>
/// При выдаче компонента сущности, он преобретает возможность привязываться к ID-карте.
/// Также может начать выполнять команды, если есть ИИ или другие компоненты для
/// взаимодействия.
/// </summary>
[RegisterComponent]
public sealed partial class CompanionComponent : Component
{
    /// <summary>
    /// Сущность ID-карты к которой привязывается компаньон, чтобы выполнять команды.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid IdCard = default;

    [ViewVariables(VVAccess.ReadWrite)]
    public string[]? MainSlots = new[] { "" };

    [ViewVariables(VVAccess.ReadWrite)]
    public string[]? OtherSlots = new[] { "id", "pocket1", "pocket2", "belt" };

    /// <summary>
    /// Проверять ли все слоты на владение ID-картой?<br/>
    /// Эта настройка заставляет компаньона выполнять команды, даже если ID-карта или
    /// КПК с ID-картой находится в слоте для КПК.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public bool IsCheckAllSlots = false;

    /// <summary>
    /// Устанавливает задачу ИИ по умолчанию.<br/>
    /// Если компаньон не имеет <see cref="HTNComponent"/>, то система компаньонов
    /// автоматически выдаст целевой сущности <see cref="HTNComponent"/> и установит <see cref="HTNComponent.RootTask"/>
    /// на значение по умолчанию при условии, что данное поле имеет значение.
    /// </summary>
    [DataField("defaultRootTask")]
    public ProtoId<HTNCompoundPrototype>? DefaultRootTask = "NonCombatantFollowerCompound";

    /// <summary>
    /// Какая задача отвечает за следования компаньона.<br/>
    /// На эту задачу будет переключаться компаньон при следовании за сущностью на которую указали.
    /// </summary>
    [DataField("followTask")]
    public ProtoId<HTNCompoundPrototype>? FollowTask = "FollowCompound";

    /// <summary>
    /// Ключ позиции сущности.<br/>
    /// Какой ключ в ИИ является позицией, чтобы начать следовать за ней.
    /// <see cref="Content.Server.NPC.HTN.Preconditions.CoordinatesNotInRangePrecondition"/>
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public string? FollowTargetKey = "FollowTarget";

    /// <summary>
    /// Расстояние на котором сущность должна находится,
    /// чтобы построить к ней машрут.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    public string? FollowRangeKey = "FollowRange";
}
