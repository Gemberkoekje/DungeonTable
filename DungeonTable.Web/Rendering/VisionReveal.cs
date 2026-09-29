using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// Computes which whole floor tiles a viewer standing at a point would see — the "fog lifter": every
/// floor tile within the vision radius (feet, at 5 ft per grid square) that has an unobstructed straight
/// line from the point, stopping at walls (and at still-hidden secret doors, which read as wall). A tile
/// only partially in view is taken as fully in view, since the reveal deals in whole tiles. This is a
/// rough field-of-view for the DM to reveal what a player with a given vision range would see from a
/// clicked spot; it is not a precise light model.
/// </summary>
public static class VisionReveal
{
    /// <summary>The floor tiles visible from a point within a vision radius, stopping at walls.</summary>
    /// <param name="map">The map (its grid, floor and walls).</param>
    /// <param name="worldX">Viewer X in world units.</param>
    /// <param name="worldY">Viewer Y in world units.</param>
    /// <param name="radiusFeet">Vision range in feet (a grid square is 5 ft).</param>
    /// <param name="secretDoorIds">Doors still hidden — they block sight like a wall.</param>
    /// <param name="openDoorIds">Doors the DM has opened — sight passes through them even where the wall
    /// geometry is drawn unbroken across the door.</param>
    /// <returns>The visible floor cells as (column, row) grid coordinates.</returns>
    public static IReadOnlyList<(int Col, int Row)> Find(
        VectorMap map, double worldX, double worldY, double radiusFeet,
        IReadOnlyCollection<string> secretDoorIds, IReadOnlyCollection<string> openDoorIds)
    {
        Grid grid = map.Grid;
        if (grid.CellSizePx <= 0 || radiusFeet <= 0)
        {
            return Array.Empty<(int, int)>();
        }

        double cell = grid.CellSizePx;
        double radius = (radiusFeet / MapMeasure.FeetPerCell) * cell;
        var floorRings = CorridorReveal.CollectRings(map, LayerKind.Floor);
        if (floorRings.Count == 0)
        {
            return Array.Empty<(int, int)>();
        }

        var walls = CorridorReveal.CollectWallSegments(map);
        CorridorReveal.AddSecretDoorSegments(walls, map, grid, secretDoorIds);
        walls = ExtendCorners(walls, cell * 0.5);
        var openGaps = CorridorReveal.CollectOpenDoorGaps(map, openDoorIds);

        // The viewer stands in the centre of the clicked tile.
        (int oc, int orow) = GridCells.CellAt(grid, worldX, worldY);
        double ox = grid.OriginX + ((oc + 0.5) * cell);
        double oy = grid.OriginY + ((orow + 0.5) * cell);

        // Any sight ray stays within the vision radius of the viewer, so walls outside that box can
        // never block one — drop them so the per-sample scan doesn't walk the whole map's walls.
        double reach = radius + cell;
        walls = CorridorReveal.SegmentsInRegion(walls, ox - reach, oy - reach, ox + reach, oy + reach);

        double radiusSq = radius * radius;
        int span = (int)Math.Ceiling(radius / cell) + 1;

        var result = new List<(int Col, int Row)>();
        for (int col = oc - span; col <= oc + span; col++)
        {
            for (int row = orow - span; row <= orow + span; row++)
            {
                MapBounds b = GridCells.CellBounds(grid, col, row);
                // Range: the tile is in reach if ANY part of it is within the radius (a partly-in-range
                // tile counts as fully in range, since the reveal deals in whole tiles).
                if (NearestDistanceSq(b, ox, oy) > radiusSq)
                {
                    continue;
                }

                if (Visible(ox, oy, b, floorRings, walls, openGaps))
                {
                    result.Add((col, row));
                }
            }
        }

        return result;
    }

    // A tile in range is visible if a straight line from the viewer reaches SOME FLOOR point of it without
    // crossing a wall. A dense 7x7 grid of sample points is tested (rather than just centre + corners), so
    // a tile a slanted wall reduces to a thin floor SLIVER still reveals — the sampler has to land on the
    // floor part, and a sliver needs the density. The centre is tried first for the common fully-floored
    // tile. Each sample must be floor AND have a clear line (Sample), so a wall-only tile or one out of
    // sight is not revealed.
    private static bool Visible(
        double ox, double oy, MapBounds b, List<IReadOnlyList<MapPoint>> floorRings,
        List<CorridorReveal.Segment> walls, List<MapBounds> openGaps)
    {
        if (Sample(ox, oy, (b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2, floorRings, walls, openGaps))
        {
            return true;
        }

        return Enumerable.Range(1, 7).Any(i =>
            Enumerable.Range(1, 7).Any(j =>
                Sample(ox, oy, b.MinX + (i / 8.0 * b.Width), b.MinY + (j / 8.0 * b.Height), floorRings, walls, openGaps)));
    }

    private static bool Sample(
        double ox, double oy, double sx, double sy, List<IReadOnlyList<MapPoint>> floorRings,
        List<CorridorReveal.Segment> walls, List<MapBounds> openGaps) =>
        CorridorReveal.PointInRings(sx, sy, floorRings) && !BlockedByWall(ox, oy, sx, sy, walls, openGaps);

    // A wall blocks the ray unless the crossing happens at an open door's opening — sight passes through
    // an open doorway even where the wall geometry is drawn unbroken across it.
    private static bool BlockedByWall(
        double ox, double oy, double sx, double sy, List<CorridorReveal.Segment> walls, List<MapBounds> openGaps)
    {
        foreach (CorridorReveal.Segment w in walls)
        {
            if (!CorridorReveal.SegmentIntersection(ox, oy, sx, sy, w.X0, w.Y0, w.X1, w.Y1, out double ix, out double iy))
            {
                continue;
            }

            if (!openGaps.Any(g => ix >= g.MinX && ix <= g.MaxX && iy >= g.MinY && iy <= g.MaxY))
            {
                return true;
            }
        }

        return false;
    }

    // Extends every wall segment slightly past each CORNER — a vertex shared by two or more segments —
    // so a sight line cannot thread through the exact point where two walls meet at a 90-degree corner and
    // leak into the next room (the strict segment-crossing test treats a ray touching a shared endpoint as
    // NOT crossing either wall). A free wall END (a doorway jamb, belonging to a single segment) is left
    // alone, so open doorways still see through. Collinear joints extend harmlessly along their own line.
    private static List<CorridorReveal.Segment> ExtendCorners(List<CorridorReveal.Segment> walls, double amount)
    {
        var shared = new Dictionary<(long, long), int>();
        foreach (CorridorReveal.Segment w in walls)
        {
            Count(shared, w.X0, w.Y0);
            Count(shared, w.X1, w.Y1);
        }

        var result = new List<CorridorReveal.Segment>(walls.Count);
        foreach (CorridorReveal.Segment w in walls)
        {
            double dx = w.X1 - w.X0, dy = w.Y1 - w.Y0;
            double len = Math.Sqrt((dx * dx) + (dy * dy));
            if (len < 1e-6)
            {
                result.Add(w);
                continue;
            }

            double ux = dx / len * amount, uy = dy / len * amount;
            double x0 = w.X0, y0 = w.Y0, x1 = w.X1, y1 = w.Y1;
            if (shared[Key(w.X0, w.Y0)] >= 2)
            {
                x0 -= ux;
                y0 -= uy;
            }

            if (shared[Key(w.X1, w.Y1)] >= 2)
            {
                x1 += ux;
                y1 += uy;
            }

            result.Add(new CorridorReveal.Segment(x0, y0, x1, y1));
        }

        return result;
    }

    private static void Count(Dictionary<(long, long), int> counts, double x, double y)
    {
        (long, long) key = Key(x, y);
        counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
    }

    private static (long, long) Key(double x, double y) =>
        ((long)Math.Round(x), (long)Math.Round(y));

    private static double NearestDistanceSq(MapBounds b, double x, double y)
    {
        double nx = Math.Clamp(x, b.MinX, b.MaxX);
        double ny = Math.Clamp(y, b.MinY, b.MaxY);
        double dx = x - nx, dy = y - ny;
        return (dx * dx) + (dy * dy);
    }
}
