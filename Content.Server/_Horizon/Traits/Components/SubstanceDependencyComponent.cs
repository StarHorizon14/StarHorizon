using System.Numerics;
using Content.Shared.Body.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._Horizon.Traits.Components;

/// <summary>
/// Dependency on a substance (alcohol, narcotics, ardon...).
/// While the entity has a matching reagent in its bloodstream the withdrawal timer is reset,
/// otherwise the withdrawal gets worse stage by stage. Stages are cumulative.
/// </summary>
[RegisterComponent]
public sealed partial class SubstanceDependencyComponent : Component
{
    /// <summary>
    /// Reagents with any of these metabolism groups satisfy the dependency.
    /// </summary>
    [DataField]
    public List<ProtoId<MetabolismGroupPrototype>> MetabolismGroups = new();

    /// <summary>
    /// Reagents that satisfy the dependency regardless of their metabolism groups.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    /// <summary>
    /// Withdrawal stages, ordered by <see cref="DependencyStage.Time"/>.
    /// </summary>
    [DataField(required: true)]
    public List<DependencyStage> Stages = new();

    /// <summary>
    /// Time without the substance after which craving popups start to appear.
    /// </summary>
    [DataField]
    public TimeSpan CravingStart = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Random delay between craving popups, (min, max) in seconds.
    /// </summary>
    [DataField]
    public Vector2 CravingInterval = new(45f, 90f);

    /// <summary>
    /// Random delay between vomiting / falling asleep incidents, (min, max) in seconds.
    /// </summary>
    [DataField]
    public Vector2 IncidentInterval = new(40f, 100f);

    /// <summary>
    /// How long a sleep incident lasts, (min, max) in seconds.
    /// </summary>
    [DataField]
    public Vector2 SleepDuration = new(8f, 20f);

    /// <summary>
    /// Localized popups shown at random while the entity craves the substance.
    /// </summary>
    [DataField]
    public List<LocId> CravingMessages = new();

    /// <summary>
    /// Popup shown when the entity gets the substance while suffering from withdrawal.
    /// </summary>
    [DataField]
    public LocId? ReliefMessage;

    [DataField]
    public float SlowdownModifier = 0.8f;

    /// <summary>
    /// Multiplier for the gun spread of the entity.
    /// </summary>
    [DataField]
    public float AimSpreadModifier = 2.5f;

    /// <summary>
    /// Damage per second for <see cref="DependencyEffect.Poison"/> and <see cref="DependencyEffect.Cellular"/>.
    /// </summary>
    [DataField]
    public float DamagePerSecond = 0.2f;

    /// <summary>
    /// Time without the substance. Only counts while the entity is alive.
    /// </summary>
    [ViewVariables]
    public TimeSpan SinceDose;

    [ViewVariables]
    public int ReachedStages;

    [ViewVariables]
    public HashSet<DependencyEffect> ActiveEffects = new();

    [ViewVariables]
    public float NextSecond = 1f;

    [ViewVariables]
    public float NextCraving;

    [ViewVariables]
    public float NextIncident;
}

[DataDefinition]
public sealed partial class DependencyStage
{
    /// <summary>
    /// Time without the substance at which this stage begins, in seconds.
    /// </summary>
    [DataField(required: true)]
    public TimeSpan Time;

    [DataField(required: true)]
    public List<DependencyEffect> Effects = new();

    /// <summary>
    /// Popup shown once when this stage is reached.
    /// </summary>
    [DataField]
    public LocId? Message;
}

public enum DependencyEffect : byte
{
    Slowdown,
    Clouding,
    AimPenalty,
    Jitter,
    Vomit,
    Sleep,
    Poison,
    Cellular,
}
