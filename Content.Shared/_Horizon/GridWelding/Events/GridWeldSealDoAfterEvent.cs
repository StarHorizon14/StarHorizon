using Content.Shared._Horizon.Shipbreaking;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Horizon.GridWelding.Events;

/// <summary>
/// Raised once the welder finishes sealing a single already-joined seam point tracked by a
/// <see cref="GridCutsComponent"/> seam.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class GridWeldSealDoAfterEvent : DoAfterEvent
{
    public NetEntity Grid;
    public Vector2i Tile;
    public CutEdges Edge;

    public GridWeldSealDoAfterEvent(NetEntity grid, Vector2i tile, CutEdges edge)
    {
        Grid = grid;
        Tile = tile;
        Edge = edge;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }

    public override bool IsDuplicate(DoAfterEvent other)
    {
        return other is GridWeldSealDoAfterEvent otherSeal
               && Grid == otherSeal.Grid
               && Tile == otherSeal.Tile
               && Edge == otherSeal.Edge;
    }
}
