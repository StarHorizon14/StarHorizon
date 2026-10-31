using Content.Shared.DoAfter;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.Shipbreaking;

[RegisterComponent, NetworkedComponent]
public sealed partial class BreachChargeComponent : Component
{
    [DataField]
    public int Segments = 5;

    [DataField]
    public float PlaceDelay = 2f;

    [DataField]
    public float EdgeTolerance = 0.5f;

    [DataField]
    public EntProtoId ArmedPrototype = "BreachChargeArmed";

    [DataField]
    public SoundSpecifier? PlaceSound = new SoundPathSpecifier("/Audio/Items/Handcuffs/rope_start.ogg");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArmedBreachChargeComponent : BaseXOnTriggerComponent
{
    public EntityUid Grid;
    public Vector2i Tile;
    public CutEdges Edge;
    public int Segments = 5;
}

[Serializable, NetSerializable]
public sealed partial class BreachChargePlaceDoAfterEvent : DoAfterEvent
{
    public NetEntity Grid;
    public Vector2i Tile;
    public CutEdges Edge;

    private BreachChargePlaceDoAfterEvent()
    {
    }

    public BreachChargePlaceDoAfterEvent(NetEntity grid, Vector2i tile, CutEdges edge)
    {
        Grid = grid;
        Tile = tile;
        Edge = edge;
    }

    public override DoAfterEvent Clone() => this;
}
