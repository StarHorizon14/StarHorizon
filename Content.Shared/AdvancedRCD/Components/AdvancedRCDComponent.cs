using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AdvancedRCD.Components;

/// <summary>
/// Component for the Advanced RCD device with material storage.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Systems.SharedAdvancedRCDSystem))]
public sealed partial class AdvancedRCDComponent : Component
{
    /// <summary>
    /// Base prototypes always available on this device (from advancedRcd prototypes).
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<AdvancedRCDPrototype>> AvailablePrototypes { get; set; } = new();

    /// <summary>
    /// Entity prototypes added from inserted machine boards.
    /// Key is entity prototype ID, value contains enabled state and material cost.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId, InsertedBoardData> InsertedEntities { get; set; } = new();

    /// <summary>
    /// Currently selected AdvancedRCD prototype ID (for base prototypes).
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<AdvancedRCDPrototype>? ProtoId { get; set; }

    /// <summary>
    /// Currently selected entity prototype ID (for inserted board entities).
    /// Only one of ProtoId or SelectedEntityProto should be set at a time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? SelectedEntityProto { get; set; }

    /// <summary>
    /// Maximum amount per material type. Null means no limit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? MaterialLimit { get; set; } = 60;

    /// <summary>
    /// Maximum interaction range.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range { get; set; } = 5f;

    /// <summary>
    /// Current construction direction for placed structures.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Direction ConstructionDirection { get; set; } = Direction.South;

    /// <summary>
    /// Default build delay for entities from boards (seconds).
    /// </summary>
    [DataField]
    public float DefaultBoardDelay { get; set; } = 3f;

    /// <summary>
    /// Base material cost added to all board constructions.
    /// Similar to FlatpackCreator's baseMachineCost.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<MaterialPrototype>, int> BaseBoardCost { get; set; } = new()
    {
        ["Steel"] = 750,
        ["Silver"] = 100,
        ["Gold"] = 50
    };

    /// <summary>
    /// Sound played on successful construction.
    /// </summary>
    [DataField]
    public SoundSpecifier SuccessSound { get; set; } = new SoundPathSpecifier("/Audio/Items/deconstruct.ogg");
}

[Serializable, NetSerializable]
public enum AdvancedRCDUiKey : byte
{
    Key
}

/// <summary>
/// Message sent when user selects an AdvancedRCD prototype.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdvancedRCDSelectMessage : BoundUserInterfaceMessage
{
    public readonly string? ProtoId;

    public AdvancedRCDSelectMessage(string? protoId)
    {
        ProtoId = protoId;
    }
}

/// <summary>
/// Message sent when user selects an entity prototype from inserted boards.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdvancedRCDSelectEntityMessage : BoundUserInterfaceMessage
{
    public readonly string? EntityProtoId;

    public AdvancedRCDSelectEntityMessage(string? entityProtoId)
    {
        EntityProtoId = entityProtoId;
    }
}

/// <summary>
/// Message sent when user toggles an inserted entity recipe on/off.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdvancedRCDToggleEntityMessage : BoundUserInterfaceMessage
{
    public readonly string EntityProtoId;
    public readonly bool Enabled;

    public AdvancedRCDToggleEntityMessage(string entityProtoId, bool enabled)
    {
        EntityProtoId = entityProtoId;
        Enabled = enabled;
    }
}

/// <summary>
/// Data for an inserted machine board in AdvancedRCD.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class InsertedBoardData
{
    /// <summary>
    /// Whether this recipe is enabled.
    /// </summary>
    [DataField]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Material cost calculated from the board's requirements.
    /// </summary>
    [DataField]
    public Dictionary<string, int> MaterialCost { get; set; } = new();

    public InsertedBoardData() { }

    public InsertedBoardData(bool enabled, Dictionary<string, int> materialCost)
    {
        Enabled = enabled;
        MaterialCost = materialCost;
    }
}

/// <summary>
/// BUI state for the Advanced RCD UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdvancedRCDBuiState : BoundUserInterfaceState
{
    public readonly Dictionary<string, List<(string ProtoId, bool CanAfford)>> Categories;
    public readonly string? SelectedProtoId;

    public AdvancedRCDBuiState(
        Dictionary<string, List<(string ProtoId, bool CanAfford)>> categories,
        string? selectedProtoId)
    {
        Categories = categories;
        SelectedProtoId = selectedProtoId;
    }
}
