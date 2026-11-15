using Content.Shared._Horizon.GridWelding.Systems;
using Content.Shared._Horizon.Shipbreaking;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;

namespace Content.Shared._Horizon.GridWelding.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGridWeldingSystem))]
public sealed partial class SeamWelderComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HasPendingSelection;

    [DataField, AutoNetworkedField]
    public NetEntity? PendingGrid;

    [DataField, AutoNetworkedField]
    public Vector2i PendingTile;

    [DataField, AutoNetworkedField]
    public CutEdges PendingEdge;

    [DataField, AutoNetworkedField]
    public TimeSpan? PendingSelectionExpiry;

    [DataField]
    public TimeSpan SelectionTimeout = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan WeldMergeDelay = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan WeldSealDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public float MergeCharge = 60f;

    [DataField]
    public float SealCharge = 15f;

    [DataField]
    public float TileProximityTolerance = 16f / 32f;

    [DataField]
    public Angle RotationSnapTolerance = Angle.FromDegrees(10);

    [DataField]
    public float EdgeSelectTolerance = 0.5f;

    [DataField]
    public float SeamTolerance = 0.2f;

    [DataField]
    public int MaxMergedGridTiles = 2000;

    [DataField]
    public SoundSpecifier WeldSound = new SoundPathSpecifier("/Audio/Items/welder.ogg");

    [DataField]
    public SoundSpecifier SealSound = new SoundPathSpecifier("/Audio/Items/welder2.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg");
}
