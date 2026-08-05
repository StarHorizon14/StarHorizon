using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.NightVision;

[RegisterComponent]
[NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(NightVisionSystem), Other = AccessPermissions.ReadWrite)]
public sealed partial class NightVisionComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isOn"), AutoNetworkedField]
    public bool IsNightVision;

    [DataField("color"), AutoNetworkedField]
    [Access(typeof(NightVisionSystem), typeof(PNVSystem), Other = AccessPermissions.Read)]
    public Color NightVisionColor = Color.Green;

    /// <summary>
    /// Цвет ночного зрения по умолчанию, восстанавливается при снятии предмета,
    /// переопределяющего <see cref="NightVisionColor"/>.
    /// </summary>
    public Color DefaultNightVisionColor = Color.Green;

    [DataField]
    public bool IsToggle = false;

    [DataField]
    public EntityUid? ActionContainer;

    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool DrawShadows = false;

    [Access(Other = AccessPermissions.ReadWriteExecute)]
    public bool GraceFrame = false;

    [DataField("transitionDuration")]
    public float TransitionDuration = 0.3f;
}

public sealed partial class NVInstantActionEvent : InstantActionEvent { }
