using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shipbreaking;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ShipCutterComponent : Component
{
    [DataField, AutoNetworkedField]
    public ShipToolMode Mode = ShipToolMode.Cut;

    [DataField, AutoNetworkedField]
    public CutEdges SelectedEdge = CutEdges.None;

    [DataField]
    public LocId CutName = "ship-tool-name-cut";

    [DataField]
    public LocId WeldName = "ship-tool-name-weld";

    [DataField]
    public LocId CutDescription = "ship-tool-desc-cut";

    [DataField]
    public SpriteSpecifier? CutSprite;

    [DataField]
    public SpriteSpecifier? WeldSprite;

    [DataField]
    public string? CutHeldPrefix;

    [DataField]
    public string? WeldHeldPrefix;

    [DataField]
    public SoundSpecifier? ChangeSound;

    [DataField]
    public LocId WeldDescription = "ship-tool-desc-weld";

    [DataField]
    public EntityWhitelist? WallWhitelist;

    [DataField]
    public float WallDelay = 4f;

    [DataField]
    public float TileDelay = 3f;

    [DataField]
    public float WallCharge = 40f;

    [DataField]
    public float TileCharge = 25f;

    [DataField]
    public float EdgeTolerance = 0.15f;

    [DataField]
    public float WallEdgeTolerance = 0.1f;

    [DataField]
    public bool RequireUtilization;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    [DataField]
    public SoundSpecifier? DoneSound = new SoundPathSpecifier("/Audio/Items/welder2.ogg");
}

[Serializable, NetSerializable]
public readonly record struct ShipCutterTarget(NetEntity Entity, Vector2i Tile, CutEdges Edge, bool IsWall)
{
    public static ShipCutterTarget Wall(NetEntity wall, CutEdges edge) => new(wall, default, edge, true);

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

[Serializable, NetSerializable]
public sealed class ShipCutterRotateEdgeEvent : EntityEventArgs
{
}

[ByRefEvent]
public readonly record struct ShipToolModeChangedEvent(ShipToolMode Mode);
