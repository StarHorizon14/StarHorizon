using Robust.Shared.GameStates;

namespace Content.Shared._Horizon.Shipbreaking;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridCutsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, CutEdges> Cuts = new();

    public const float CutWidthPixels = 3f;

    public bool IsCut(Vector2i tile, CutEdges edge)
    {
        return Cuts.GetValueOrDefault(tile).HasFlag(edge) ||
               Cuts.GetValueOrDefault(tile + edge.Offset()).HasFlag(edge.Opposite());
    }
}

[Flags]
public enum CutEdges : byte
{
    None = 0,
    North = 1 << 0,
    East = 1 << 1,
    South = 1 << 2,
    West = 1 << 3,
}

public static class CutEdgesExtensions
{
    public static readonly CutEdges[] All = { CutEdges.North, CutEdges.East, CutEdges.South, CutEdges.West };

    public static CutEdges Opposite(this CutEdges edge)
    {
        return edge switch
        {
            CutEdges.North => CutEdges.South,
            CutEdges.South => CutEdges.North,
            CutEdges.East => CutEdges.West,
            CutEdges.West => CutEdges.East,
            _ => CutEdges.None,
        };
    }

    /// The direction the seam of this edge runs in: a horizontal seam (north, south) runs along x, a vertical one along y.
    public static Vector2i Along(this CutEdges edge)
    {
        return edge is CutEdges.North or CutEdges.South ? new Vector2i(1, 0) : new Vector2i(0, 1);
    }

    public static CutEdges FromOffset(Vector2i offset)
    {
        if (offset == new Vector2i(0, 1))
            return CutEdges.North;
        if (offset == new Vector2i(1, 0))
            return CutEdges.East;
        if (offset == new Vector2i(0, -1))
            return CutEdges.South;
        if (offset == new Vector2i(-1, 0))
            return CutEdges.West;

        return CutEdges.None;
    }

    private static readonly CutEdges[] CounterClockwise = { CutEdges.North, CutEdges.West, CutEdges.South, CutEdges.East };

    public static CutEdges Rotate(this CutEdges edges, int quarterTurns)
    {
        quarterTurns = ((quarterTurns % 4) + 4) % 4;
        if (quarterTurns == 0 || edges == CutEdges.None)
            return edges;

        var result = CutEdges.None;
        for (var i = 0; i < CounterClockwise.Length; i++)
        {
            if (edges.HasFlag(CounterClockwise[i]))
                result |= CounterClockwise[(i + quarterTurns) % 4];
        }

        return result;
    }

    public static Box2 GetRect(this CutEdges edge, Vector2i tile, float tileSize, float halfWidth)
    {
        var x = tile.X * tileSize;
        var y = tile.Y * tileSize;

        return edge switch
        {
            CutEdges.North => new Box2(x, y + tileSize - halfWidth, x + tileSize, y + tileSize + halfWidth),
            CutEdges.South => new Box2(x, y - halfWidth, x + tileSize, y + halfWidth),
            CutEdges.East => new Box2(x + tileSize - halfWidth, y, x + tileSize + halfWidth, y + tileSize),
            _ => new Box2(x - halfWidth, y, x + halfWidth, y + tileSize),
        };
    }

    public static Vector2i Offset(this CutEdges edge)
    {
        return edge switch
        {
            CutEdges.North => new Vector2i(0, 1),
            CutEdges.South => new Vector2i(0, -1),
            CutEdges.East => new Vector2i(1, 0),
            CutEdges.West => new Vector2i(-1, 0),
            _ => Vector2i.Zero,
        };
    }
}
