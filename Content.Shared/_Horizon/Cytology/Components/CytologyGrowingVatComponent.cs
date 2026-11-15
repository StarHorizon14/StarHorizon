using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Horizon.Cytology.Components;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class CytologyGrowingVatComponent : Component
{
    [DataField]
    public ItemSlot PetriDishSlot = new();

    [DataField]
    public ItemSlot BeakerSlot = new();

    /// <summary>
    ///     A toggle state that indicates whether the vat will work
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive = false;

    /// <summary>
    ///     Indicates that an error occurred during cell cultivation. Responsible for switching the indicator
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool StopWithError = false;

    /// <summary>
    ///     An auxiliary field for showing foam
    /// </summary>
    [DataField]
    public bool WithFoam = false;

    [DataField, AutoNetworkedField]
    public bool IsPowered = false;

    [DataField]
    public EntProtoId SmokePrototype = new();

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField, AutoPausedField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1f);

    /// <summary>
    ///     Horizon: reagents that kill viruses in the samples. Each virus takes 1u of them
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> Antivirals = new() { "Ultravasculine", "Stellibinin", "Dylovene" };

    /// <summary>
    ///     Horizon: how much each virus slows the growth down, multiplied by the cell's viral susceptibility
    /// </summary>
    [DataField]
    public float VirusGrowthPenalty = 0.25f;

}
