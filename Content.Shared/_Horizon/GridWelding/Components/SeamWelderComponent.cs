using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Horizon.GridWelding.Components;

/// <summary>
/// The seam welding mode of the ship tool (see <see cref="ShipCutterComponent"/>). Can splice two separate grids together into one, and afterwards weld shut
/// the remaining seam points along the newly-joined border one at a time. See
/// <see cref="SharedGridWeldingSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGridWeldingSystem))]
public sealed partial class SeamWelderComponent : Component
{
    /// <summary>
    /// Whether the player has already picked a first point (on some grid) and is now waiting on
    /// a second point (on a different grid) to complete the weld.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool HasPendingSelection;

    /// <summary>
    /// The grid the first selected point is on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public NetEntity? PendingGrid;

    /// <summary>
    /// The tile index (on <see cref="PendingGrid"/>) of the first selected point.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2i PendingTile;

    /// <summary>
    /// The edge of <see cref="PendingTile"/> that is to be joined.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CutEdges PendingEdge;

    /// <summary>
    /// When the pending first selection expires if a second point isn't picked in time.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? PendingSelectionExpiry;

    /// <summary>
    /// How long a first selection stays armed before it needs to be picked again.
    /// </summary>
    [DataField]
    public TimeSpan SelectionTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// DoAfter delay for the initial weld that merges two grids into one.
    /// </summary>
    [DataField]
    public TimeSpan WeldMergeDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// DoAfter delay for sealing a single already-joined seam point.
    /// </summary>
    [DataField]
    public TimeSpan WeldSealDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Charge taken from the power cell for the initial grid-merging weld.
    /// </summary>
    [DataField]
    public float MergeCharge = 60f;

    /// <summary>
    /// Charge taken from the power cell for sealing a single seam point.
    /// </summary>
    [DataField]
    public float SealCharge = 15f;

    /// <summary>
    /// How far apart (in world meters) the two selected tiles' facing edges are allowed to be for
    /// a weld to be attempted. 16px (half a tile) at the standard 32px-per-tile scale: the grids snap onto each other's tile grid.
    /// </summary>
    [DataField]
    public float TileProximityTolerance = 16f / 32f;

    /// <summary>
    /// How far off a clean 90-degree snap the two grids' relative rotation is allowed to be.
    /// </summary>
    [DataField]
    public Angle RotationSnapTolerance = Angle.FromDegrees(10);

    /// <summary>
    /// How close to a tile edge a click has to be to pick that edge for welding, in tiles. Half a tile always picks the
    /// nearest edge of the clicked tile.
    /// </summary>
    [DataField]
    public float EdgeSelectTolerance = 0.5f;

    /// <summary>
    /// How close to a cut seam between two tiles a click has to be to weld it shut, in tiles.
    /// </summary>
    [DataField]
    public float SeamTolerance = 0.2f;

    /// <summary>
    /// Maximum combined tile count the resulting grid is allowed to have after a merge.
    /// </summary>
    [DataField]
    public int MaxMergedGridTiles = 2000;

    [DataField]
    public SoundSpecifier WeldSound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    [DataField]
    public SoundSpecifier SealSound = new SoundPathSpecifier("/Audio/Items/welder2.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg");
}
