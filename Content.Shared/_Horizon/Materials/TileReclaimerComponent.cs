using Content.Shared._NF.Bank.Components;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Horizon.Materials;

/// <summary>
/// Makes an entity into a grid reclaimer, able to delete grid tiles in front of it and produce materials.
/// Ported from space-wizards/space-station-14#43651.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
[Access(typeof(TileReclaimerSystem))]
public sealed partial class TileReclaimerComponent : Component
{
    /// <summary>
    /// An "enable" toggle for things like interfacing with machine linking.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// A master control for whether or not the reclaimer is broken and can function.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;

    /// <summary>
    /// A whitelist for what grid entities can be affected by this reclaimer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// A blacklist for what grid entities can not be affected by this reclaimer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// The delay after a grid has been consumed that it can recycle again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan RecycleDelay = TimeSpan.FromSeconds(0.3f);

    /// <summary>
    /// The bounding box in local coordinates for where it will check for grids to reclaim.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Box2 RecyclingBox = new(-0.5f, 0.5f, 0.5f, 1.5f);

    /// <summary>
    /// Multiplier for the value being extracted from the tile, as defined in the tile's <see cref="ContentTileDefinition.MaterialComposition"/>
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Efficiency = 1f;

    /// <summary>
    /// Whether reclaimed tiles are paid for. A furnace burns them for nothing.
    /// </summary>
    [DataField]
    public bool Pay = true;

    /// <summary>
    /// The bank account that receives the money for reclaimed tiles.
    /// </summary>
    [DataField]
    public SectorBankAccount Account = SectorBankAccount.Frontier;

    /// <summary>
    /// How strong impulse the reclaimer should apply to a grid after it has destroyed one of its tiles.
    /// This helps guide the grid further into the reclaimer.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SlurpStrength = 1600f;

    /// <summary>
    /// Next time a recycling attempt can be made.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadOnly)]
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextRecycle;

    /// <summary>
    /// The sound played when something is being processed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound;
}
