using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Horizon.NightVision;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class PNVComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))] public string ActionProto = "NVToggleAction";
    [DataField] public EntityUid? ActionContainer;

    /// <summary>
    /// Если задан — цвет ночного зрения носителя переопределяется этим цветом
    /// на время ношения предмета.
    /// </summary>
    [DataField] public Color? Color;
}
