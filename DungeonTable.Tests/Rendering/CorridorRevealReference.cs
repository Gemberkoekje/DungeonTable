using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Tests.Rendering;

/// <summary>
/// The projector's corridor peek as it was before it used <see cref="VisionIndex"/>: every step's floor
/// test against every floor outline, every wall test against every wall on the map. Kept, unchanged but
/// for the name, as the reference the fast <see cref="CorridorReveal.Find"/> must agree with; too slow
/// for the table, and used by the tests only.
/// </summary>
internal static class CorridorRevealReference
{
    /// <summary>Cells reachable from the revealed area within <paramref name="steps"/>, in world units.</summary>
    /// <param name="map">The map (its floor bounds passability and walls block the fill).</param>
    /// <param name="regions">Revealed room polygons.</param>
    /// <param name="cells">Revealed fog-brush cells.</param>
    /// <param name="steps">How many cells out to reach (1–2 keeps the peek short).</param>
    /// <param name="secretDoorIds">Doors still secret — they block the fill like a wall, so the peek
    /// never runs past one and hints at the passage behind it.</param>
    /// <param name="openDoorIds">Doors the DM has opened — they are a passage, so the fill runs through
    /// them (revealing the tiles behind) even where the wall geometry is unbroken across the door.</param>
    /// <returns>The newly-reached cells with their step distance (1..steps), excluding revealed cells.</returns>
    internal static IReadOnlyList<(MapBounds Cell, int Distance)> Find(
        VectorMap map,
        IReadOnlyList<IReadOnlyList<MapPoint>> regions,
        IReadOnlyList<MapBounds> cells,
        int steps,
        IReadOnlyCollection<string> secretDoorIds,
        IReadOnlyCollection<string> openDoorIds)
    {
        Grid grid = map.Grid;
        if (grid.CellSizePx <= 0 || steps <= 0)
        {
            return Array.Empty<(MapBounds, int)>();
        }

        List<IReadOnlyList<MapPoint>> floorRings = CorridorReveal.CollectRings(map, LayerKind.Floor);
        if (floorRings.Count == 0)
        {
            return Array.Empty<(MapBounds, int)>();
        }

        List<CorridorReveal.Segment> wallSegments = CorridorReveal.CollectWallSegments(map);
        CorridorReveal.AddSecretDoorSegments(wallSegments, map, grid, secretDoorIds);
        var openGaps = CorridorReveal.CollectOpenDoorGaps(map, openDoorIds);

        var seen = new HashSet<(int Col, int Row)>();
        var queue = new Queue<((int Col, int Row) Cell, int Dist)>();
        foreach ((int Col, int Row) start in StartCells(grid, regions, cells))
        {
            if (!seen.Add(start))
            {
                continue;
            }

            queue.Enqueue((start, 0));
        }

        var result = new List<(MapBounds Cell, int Distance)>();
        (int Dc, int Dr)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
        while (queue.Count > 0)
        {
            ((int Col, int Row) cell, int dist) = queue.Dequeue();
            if (dist >= steps)
            {
                continue;
            }

            foreach ((int dc, int dr) in dirs)
            {
                (int Col, int Row) next = (cell.Col + dc, cell.Row + dr);
                if (seen.Contains(next)
                    || !IsFloorCell(grid, next, floorRings)
                    || WallBetween(grid, cell, next, wallSegments, openGaps))
                {
                    continue;
                }

                seen.Add(next);
                queue.Enqueue((next, dist + 1));
                result.Add((GridCells.CellBounds(grid, next.Col, next.Row), dist + 1));
            }
        }

        return result;
    }

    private static IEnumerable<(int Col, int Row)> StartCells(
        Grid grid, IReadOnlyList<IReadOnlyList<MapPoint>> regions, IReadOnlyList<MapBounds> cells)
    {
        double size = grid.CellSizePx;
        foreach (MapBounds cell in cells)
        {
            double cx = (cell.MinX + cell.MaxX) / 2;
            double cy = (cell.MinY + cell.MaxY) / 2;
            yield return GridCells.CellAt(grid, cx, cy);
        }

        foreach (IReadOnlyList<MapPoint> region in regions)
        {
            if (region.Count < 3)
            {
                continue;
            }

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (MapPoint p in region)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }

            (int colLo, int rowLo) = GridCells.CellAt(grid, minX, minY);
            (int colHi, int rowHi) = GridCells.CellAt(grid, maxX, maxY);
            for (int col = colLo; col <= colHi; col++)
            {
                for (int row = rowLo; row <= rowHi; row++)
                {
                    double cx = grid.OriginX + ((col + 0.5) * size);
                    double cy = grid.OriginY + ((row + 0.5) * size);
                    if (CorridorReveal.PointInRings(cx, cy, new[] { region }))
                    {
                        yield return (col, row);
                    }
                }
            }
        }
    }

    private static bool IsFloorCell(Grid grid, (int Col, int Row) cell, List<IReadOnlyList<MapPoint>> floorRings)
    {
        double size = grid.CellSizePx;
        double cx = grid.OriginX + ((cell.Col + 0.5) * size);
        double cy = grid.OriginY + ((cell.Row + 0.5) * size);
        return CorridorReveal.PointInRings(cx, cy, floorRings);
    }

    // The fill is blocked when a wall lies between the two cells. Test the segment joining their
    // centres (which crosses a wall perpendicularly) rather than the shared grid edge — walls run
    // ALONG grid lines, so a shared-edge test would be collinear with them and miss. An OPEN door
    // punches a passage: if the crossing (the segment midpoint) falls inside an open door's opening,
    // the fill runs through regardless of the wall drawn there.
    private static bool WallBetween(
        Grid grid, (int Col, int Row) a, (int Col, int Row) b, List<CorridorReveal.Segment> walls, List<MapBounds> openGaps)
    {
        double size = grid.CellSizePx;
        double ax = grid.OriginX + ((a.Col + 0.5) * size);
        double ay = grid.OriginY + ((a.Row + 0.5) * size);
        double bx = grid.OriginX + ((b.Col + 0.5) * size);
        double by = grid.OriginY + ((b.Row + 0.5) * size);

        double mx = (ax + bx) / 2, my = (ay + by) / 2;
        if (openGaps.Any(g => mx >= g.MinX && mx <= g.MaxX && my >= g.MinY && my <= g.MaxY))
        {
            return false;
        }

        return walls.Any(w => CorridorReveal.SegmentsIntersect(ax, ay, bx, by, w.X0, w.Y0, w.X1, w.Y1));
    }
}
