using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Horizon.Shuttles;

/// <summary>
/// Landing pad tiles (<see cref="ContentTileDefinition.GridUnderlay"/>) have no collision fixtures,
/// so code that finds a grid's area from its fixtures misses them. This gives that area directly.
/// </summary>
public static class LandingPadTiles
{
    /// <summary>
    /// Fills <paramref name="boxes"/> with the grid-local boxes covering the grid's landing pad tiles,
    /// one box per run of pad tiles in a row. Returns whether there are any.
    /// </summary>
    public static bool GetPadBoxes(
        Entity<MapGridComponent> grid,
        SharedMapSystem maps,
        ITileDefinitionManager tileDefs,
        List<Box2> boxes)
    {
        boxes.Clear();
        var rows = new Dictionary<int, List<int>>();
        var tiles = maps.GetAllTilesEnumerator(grid, grid.Comp);

        while (tiles.MoveNext(out var tile))
        {
            if (!tileDefs.TryGetDefinition(tile.Value.Tile.TypeId, out var def) ||
                def is not ContentTileDefinition { GridUnderlay: true })
            {
                continue;
            }

            var index = tile.Value.GridIndices;

            if (!rows.TryGetValue(index.Y, out var row))
            {
                row = new List<int>();
                rows[index.Y] = row;
            }

            row.Add(index.X);
        }

        var size = (float) grid.Comp.TileSize;

        foreach (var (y, xs) in rows)
        {
            xs.Sort();
            var start = xs[0];
            var previous = xs[0];

            for (var i = 1; i <= xs.Count; i++)
            {
                if (i < xs.Count && xs[i] == previous + 1)
                {
                    previous = xs[i];
                    continue;
                }

                boxes.Add(new Box2(start * size, y * size, (previous + 1) * size, (y + 1) * size));

                if (i < xs.Count)
                {
                    start = xs[i];
                    previous = xs[i];
                }
            }
        }

        return boxes.Count > 0;
    }
}
