using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.GridWelding.Events;

[Serializable, NetSerializable]
public sealed partial class GridWeldMergeDoAfterEvent : DoAfterEvent
{
    public NetEntity GridA;
    public Vector2i TileA;
    public CutEdges EdgeA;
    public NetEntity GridB;
    public Vector2i TileB;
    public CutEdges EdgeB;

    public GridWeldMergeDoAfterEvent(NetEntity gridA, Vector2i tileA, CutEdges edgeA, NetEntity gridB, Vector2i tileB, CutEdges edgeB)
    {
        GridA = gridA;
        TileA = tileA;
        EdgeA = edgeA;
        GridB = gridB;
        TileB = tileB;
        EdgeB = edgeB;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is GridWeldMergeDoAfterEvent otherMerge
               && GridA == otherMerge.GridA
               && TileA == otherMerge.TileA
               && EdgeA == otherMerge.EdgeA
               && GridB == otherMerge.GridB
               && TileB == otherMerge.TileB
               && EdgeB == otherMerge.EdgeB;
    }
}
