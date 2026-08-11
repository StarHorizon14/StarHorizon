using Robust.Shared.GameStates;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Injects a solution stored on this entity into the target's bloodstream when triggered.
/// If TargetUser is true the user of the trigger will be injected instead of the owner.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class InjectOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Name of the solution (defined via SolutionContainerManager on this entity) to inject from.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string Solution = "injection";
}
