using Content.Shared.Materials;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon._Fractions.AnCo.Biofabricator;

[RegisterComponent]
public sealed partial class AnCoBiofabricatorComponent : Component
{
    [DataField]
    public string CardSlotId = "biofab-cardSlot";

    /// <summary>
    /// Holds the restored body while the restoration animation/timer is running, mirroring
    /// CloningPodComponent.BodyContainer.
    /// </summary>
    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    /// <summary>
    /// The material that is consumed to restore a body.
    /// </summary>
    [DataField]
    public ProtoId<MaterialPrototype> RequiredMaterial = "Biomass";

    /// <summary>
    /// How much of <see cref="RequiredMaterial"/> is consumed per restoration.
    /// </summary>
    [DataField]
    public int BiomassCost = 70;

    /// <summary>
    /// How long the restoration takes.
    /// </summary>
    [DataField]
    public float RestoreTime = 30f;

    [ViewVariables]
    public float RestoreProgress;

    [ViewVariables(VVAccess.ReadWrite)]
    public AnCoBiofabricatorStatus Status;
}

[Serializable, NetSerializable]
public enum AnCoBiofabricatorVisuals : byte
{
    Status
}

[Serializable, NetSerializable]
public enum AnCoBiofabricatorStatus : byte
{
    Idle,
    Restoring,
    Complete
}
