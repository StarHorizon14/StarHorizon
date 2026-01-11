using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Paws;

/// <summary>
/// Компонент, который блокирует поднятие предметов с указанными тегами.
/// Добавляется на сущность, которая не может поднимать предметы с этими тегами.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PawsComponent : Component
{
    /// <summary>
    /// Список тегов, предметы с которыми не могут быть подняты.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TagPrototype>> BlockedTags = new();
}
