using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shipbreaking;

/// <summary>
/// A battery powered shipbreaking tool with two modes, switched by using it in hand.
/// In <see cref="ShipToolMode.Cut"/> clicking a wall cuts it loose so it can be moved with a gravity gun, and clicking
/// the seam between two floor tiles cuts a slit along it (see <see cref="GridCutsComponent"/>).
/// In <see cref="ShipToolMode.Weld"/> it works as a seam welder (see the SeamWelder component).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ShipCutterComponent : Component
{
    /// <summary>
    /// What the tool does at the moment. The name of the item follows it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ShipToolMode Mode = ShipToolMode.Cut;

    /// <summary>
    /// Name of the item in each mode, localized.
    /// </summary>
    [DataField]
    public LocId CutName = "ship-tool-name-cut";

    [DataField]
    public LocId WeldName = "ship-tool-name-weld";

    [DataField]
    public LocId CutDescription = "ship-tool-desc-cut";

    /// <summary>
    /// How the item looks in each mode (inventory and floor icon). Not set: the sprite of the prototype stays.
    /// </summary>
    [DataField]
    public SpriteSpecifier? CutSprite;

    [DataField]
    public SpriteSpecifier? WeldSprite;

    /// <summary>
    /// Prefix of the in-hand states in each mode (<c>{prefix}-inhand-left</c> and so on). Not set: the prefix stays.
    /// </summary>
    [DataField]
    public string? CutHeldPrefix;

    [DataField]
    public string? WeldHeldPrefix;

    /// <summary>
    /// Played when the mode is switched.
    /// </summary>
    [DataField]
    public SoundSpecifier? ChangeSound;

    [DataField]
    public LocId WeldDescription = "ship-tool-desc-weld";

    /// <summary>
    /// What counts as a wall that can be cut loose.
    /// </summary>
    [DataField]
    public EntityWhitelist? WallWhitelist;

    [DataField]
    public float WallDelay = 4f;

    [DataField]
    public float TileDelay = 3f;

    /// <summary>
    /// Charge taken from the power cell for cutting a wall loose.
    /// </summary>
    [DataField]
    public float WallCharge = 40f;

    /// <summary>
    /// Charge taken from the power cell for one cut between tiles.
    /// </summary>
    [DataField]
    public float TileCharge = 25f;

    /// <summary>
    /// How close to a tile edge the click has to be to count as a click on the seam, in tiles.
    /// </summary>
    [DataField]
    public float EdgeTolerance = 0.15f;

    /// <summary>
    /// Same as <see cref="EdgeTolerance"/> on a tile with a wall: narrower, so most of the wall is a click on the wall.
    /// </summary>
    [DataField]
    public float WallEdgeTolerance = 0.1f;

    /// <summary>
    /// Played when a cut starts.
    /// </summary>
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    /// <summary>
    /// Played when a cut is done.
    /// </summary>
    [DataField]
    public SoundSpecifier? DoneSound = new SoundPathSpecifier("/Audio/Items/welder2.ogg");
}

/// <summary>
/// One thing to cut: an edge of a wall, or an edge of a tile on a grid.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct ShipCutterTarget(NetEntity Entity, Vector2i Tile, CutEdges Edge, bool IsWall)
{
    /// <summary>
    /// The entity is the wall. An edge of <see cref="CutEdges.None"/> means the wall is free already and just comes loose.
    /// </summary>
    public static ShipCutterTarget Wall(NetEntity wall, CutEdges edge) => new(wall, default, edge, true);

    /// <summary>
    /// For a seam the entity is the grid.
    /// </summary>
    public static ShipCutterTarget Seam(NetEntity grid, Vector2i tile, CutEdges edge) => new(grid, tile, edge, false);
}

[Serializable, NetSerializable]
public sealed partial class ShipCutterDoAfterEvent : DoAfterEvent
{
    public ShipCutterTarget Cut;

    private ShipCutterDoAfterEvent()
    {
    }

    public ShipCutterDoAfterEvent(ShipCutterTarget cut)
    {
        Cut = cut;
    }

    public override DoAfterEvent Clone() => this;
}

public enum ShipToolMode : byte
{
    Cut,
    Weld,
}

/// <summary>
/// Raised on the tool after its mode was switched.
/// </summary>
[ByRefEvent]
public readonly record struct ShipToolModeChangedEvent(ShipToolMode Mode);
