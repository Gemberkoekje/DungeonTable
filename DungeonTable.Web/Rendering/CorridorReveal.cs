using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// Finds the grid cells just beyond a revealed area that are reachable through open doorways —
/// a flood fill over the map's floor that never crosses a wall. The player view renders these as
/// short corridor stubs fading into darkness, so players see where the exits lead a step or two
/// while a wall still hides whatever room lies behind it (the fill simply can't reach it).
/// </summary>
public static class CorridorReveal
{
    internal readonly record struct Segment(double X0, double Y0, double X1, double Y1);

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
    public static IReadOnlyList<(MapBounds Cell, int Distance)> Find(
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

        var floorRings = CollectRings(map, LayerKind.Floor);
        if (floorRings.Count == 0)
        {
            return Array.Empty<(MapBounds, int)>();
        }

        var wallSegments = CollectWallSegments(map);
        AddSecretDoorSegments(wallSegments, map, grid, secretDoorIds);
        var openGaps = CollectOpenDoorGaps(map, openDoorIds);

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

    /// <summary>The revealed grid cells (inside a region or a brush cell) as world-unit rectangles.</summary>
    /// <param name="map">The map (its grid sizes the cells).</param>
    /// <param name="regions">Revealed room polygons.</param>
    /// <param name="cells">Revealed fog-brush cells.</param>
    /// <returns>The distinct shown cells, in world units.</returns>
    public static IReadOnlyList<MapBounds> ShownCells(
        VectorMap map,
        IReadOnlyList<IReadOnlyList<MapPoint>> regions,
        IReadOnlyList<MapBounds> cells)
    {
        Grid grid = map.Grid;
        if (grid.CellSizePx <= 0)
        {
            return Array.Empty<MapBounds>();
        }

        var seen = new HashSet<(int Col, int Row)>();
        var rects = new List<MapBounds>();
        foreach ((int col, int row) in StartCells(grid, regions, cells))
        {
            if (seen.Add((col, row)))
            {
                rects.Add(GridCells.CellBounds(grid, col, row));
            }
        }

        return rects;
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
                    if (PointInRings(cx, cy, new[] { region }))
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
        return PointInRings(cx, cy, floorRings);
    }

    // The fill is blocked when a wall lies between the two cells. Test the segment joining their
    // centres (which crosses a wall perpendicularly) rather than the shared grid edge — walls run
    // ALONG grid lines, so a shared-edge test would be collinear with them and miss. An OPEN door
    // punches a passage: if the crossing (the segment midpoint) falls inside an open door's opening,
    // the fill runs through regardless of the wall drawn there.
    private static bool WallBetween(
        Grid grid, (int Col, int Row) a, (int Col, int Row) b, List<Segment> walls, List<MapBounds> openGaps)
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

        return walls.Any(w => SegmentsIntersect(ax, ay, bx, by, w.X0, w.Y0, w.X1, w.Y1));
    }

    // Opening rectangles (slightly inflated so a cell-edge crossing lands inside) of the open doors, so
    // the fill can pass through them like a gap in the wall.
    internal static List<MapBounds> CollectOpenDoorGaps(VectorMap map, IReadOnlyCollection<string> openDoorIds)
    {
        if (openDoorIds.Count == 0)
        {
            return new List<MapBounds>();
        }

        double ext = map.Grid.CellSizePx * 0.25;
        return map.Doors
            .Where(d => openDoorIds.Contains(d.Id))
            .Select(d => new MapBounds(d.Bounds.MinX - ext, d.Bounds.MinY - ext, d.Bounds.MaxX + ext, d.Bounds.MaxY + ext))
            .ToList();
    }

    internal static List<IReadOnlyList<MapPoint>> CollectRings(VectorMap map, LayerKind kind)
    {
        if (!TryFindLayer(map.Layers, kind, out MapLayer layer))
        {
            return new List<IReadOnlyList<MapPoint>>();
        }

        return layer.Geometry.Polygons
            .SelectMany(polygon => polygon.Rings)
            .Select(ring => ring.Vertices)
            .Where(vertices => vertices.Count >= 3)
            .ToList();
    }

    internal static List<Segment> CollectWallSegments(VectorMap map)
    {
        var segments = new List<Segment>();
        if (!TryFindLayer(map.Layers, LayerKind.Walls, out MapLayer walls))
        {
            return segments;
        }

        foreach (IReadOnlyList<MapPoint> ring in walls.Geometry.Polygons.SelectMany(p => p.Rings).Select(r => r.Vertices))
        {
            AddChain(segments, ring, closed: true);
        }

        foreach (Polyline polyline in walls.Geometry.Polylines)
        {
            AddChain(segments, polyline.Points, closed: false);
        }

        return segments;
    }

    // A secret door is a gap in the wall geometry (its floor is walkable), so the fill would otherwise
    // stroll straight through it and reveal the corridor beyond — betraying the hidden door. Lay a
    // blocking segment across each secret door, along its wall, so it stops the fill exactly like wall.
    internal static void AddSecretDoorSegments(
        List<Segment> segments, VectorMap map, Grid grid, IReadOnlyCollection<string> secretDoorIds)
    {
        if (secretDoorIds.Count == 0)
        {
            return;
        }

        double ext = grid.CellSizePx * 0.5;
        foreach (DoorFeature door in map.Doors)
        {
            if (!secretDoorIds.Contains(door.Id))
            {
                continue;
            }

            MapBounds b = door.Bounds;
            double cx = (b.MinX + b.MaxX) / 2;
            double cy = (b.MinY + b.MaxY) / 2;
            bool vertical = door.Orientation == DoorOrientation.Vertical
                || (door.Orientation == DoorOrientation.None && b.Height >= b.Width);

            // Extend half a cell past each jamb so the segment reliably spans the neighbouring cells'
            // centre-to-centre test even when the opening is narrower than a cell.
            if (vertical)
            {
                segments.Add(new Segment(cx, b.MinY - ext, cx, b.MaxY + ext));
            }
            else
            {
                segments.Add(new Segment(b.MinX - ext, cy, b.MaxX + ext, cy));
            }
        }
    }

    // Wall segments whose bounding box overlaps the query rectangle — lets a local vision/reveal test
    // skip the far-away walls it can never cross, so the per-sample scan stays cheap on a big map.
    internal static List<Segment> SegmentsInRegion(
        List<Segment> segments, double minX, double minY, double maxX, double maxY) =>
        segments.Where(w =>
            Math.Min(w.X0, w.X1) <= maxX && Math.Max(w.X0, w.X1) >= minX
            && Math.Min(w.Y0, w.Y1) <= maxY && Math.Max(w.Y0, w.Y1) >= minY).ToList();

    private static void AddChain(List<Segment> segments, IReadOnlyList<MapPoint> points, bool closed)
    {
        for (int i = 0; i + 1 < points.Count; i++)
        {
            segments.Add(new Segment(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y));
        }

        if (closed && points.Count > 2)
        {
            MapPoint first = points[0];
            MapPoint last = points[points.Count - 1];
            segments.Add(new Segment(last.X, last.Y, first.X, first.Y));
        }
    }

    internal static bool PointInRings(double x, double y, IReadOnlyList<IReadOnlyList<MapPoint>> rings)
    {
        bool inside = false;
        foreach (IReadOnlyList<MapPoint> ring in rings)
        {
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                MapPoint a = ring[i];
                MapPoint b = ring[j];
                if (((a.Y > y) != (b.Y > y))
                    && (x < ((b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) + a.X))
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }

    internal static bool SegmentsIntersect(
        double ax0, double ay0, double ax1, double ay1,
        double bx0, double by0, double bx1, double by1)
    {
        double d1 = Cross(bx0, by0, bx1, by1, ax0, ay0);
        double d2 = Cross(bx0, by0, bx1, by1, ax1, ay1);
        double d3 = Cross(ax0, ay0, ax1, ay1, bx0, by0);
        double d4 = Cross(ax0, ay0, ax1, ay1, bx1, by1);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
            && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    // Like SegmentsIntersect but also returns the crossing point, so a caller can tell whether a sight
    // ray's wall crossing falls at an open door (which must not block) or elsewhere (which does).
    internal static bool SegmentIntersection(
        double ax0, double ay0, double ax1, double ay1,
        double bx0, double by0, double bx1, double by1,
        out double ix, out double iy)
    {
        ix = 0;
        iy = 0;
        double d1x = ax1 - ax0, d1y = ay1 - ay0;
        double d2x = bx1 - bx0, d2y = by1 - by0;
        double denom = (d1x * d2y) - (d1y * d2x);
        if (Math.Abs(denom) < 1e-9)
        {
            return false;
        }

        double t = (((bx0 - ax0) * d2y) - ((by0 - ay0) * d2x)) / denom;
        double u = (((bx0 - ax0) * d1y) - ((by0 - ay0) * d1x)) / denom;
        if (t < 0 || t > 1 || u < 0 || u > 1)
        {
            return false;
        }

        ix = ax0 + (t * d1x);
        iy = ay0 + (t * d1y);
        return true;
    }

    private static double Cross(double ox, double oy, double ax, double ay, double bx, double by) =>
        ((ax - ox) * (by - oy)) - ((ay - oy) * (bx - ox));

    private static bool TryFindLayer(IReadOnlyList<MapLayer> layers, LayerKind kind, out MapLayer found)
    {
        foreach (MapLayer layer in layers)
        {
            if (layer.Kind == kind)
            {
                found = layer;
                return true;
            }

            if (layer.Children.Count > 0 && TryFindLayer(layer.Children, kind, out found))
            {
                return true;
            }
        }

        found = new MapLayer();
        return false;
    }
}
