using System.Collections.Generic;
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
/// <remarks>
/// What never changes about the map (which sample points are floor, the walls and where they meet) is
/// worked out once per map in a <see cref="VisionIndex"/>; each lift files the walls within reach in
/// <see cref="SightLines"/>. Both give exactly the answers the plain tests did, only without testing every
/// point against the whole map: a 120 ft lift on a large level went from seconds to milliseconds.
/// </remarks>
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

        VisionIndex index = VisionIndex.For(map);
        if (!index.HasFloor)
        {
            return Array.Empty<(int, int)>();
        }

        double cell = grid.CellSizePx;
        double radius = (radiusFeet / MapMeasure.FeetPerCell) * cell;

        // The viewer stands in the centre of the clicked tile.
        (int oc, int orow) = GridCells.CellAt(grid, worldX, worldY);
        double ox = grid.OriginX + ((oc + 0.5) * cell);
        double oy = grid.OriginY + ((orow + 0.5) * cell);

        // Any sight ray stays within the vision radius of the viewer (plus the far tile), so walls outside
        // that box can never block one.
        SightLines sight = SightLines.Around(index, map, ox, oy, radius + cell, secretDoorIds, openDoorIds);

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

                ulong floor = index.FloorMask(col, row);
                if (floor != 0 && Visible(ox, oy, b, floor, sight))
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
    // tile. Only the sample points the index says are floor are tried, so a wall-only tile or one out of
    // sight is not revealed.
    private static bool Visible(double ox, double oy, MapBounds b, ulong floor, SightLines sight)
    {
        if ((floor & (1UL << VisionIndex.CentreBit)) != 0
            && !sight.Blocked(ox, oy, (b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2))
        {
            return true;
        }

        for (int i = 1; i <= VisionIndex.Lattice; i++)
        {
            double x = b.MinX + (i / 8.0 * b.Width);
            for (int j = 1; j <= VisionIndex.Lattice; j++)
            {
                int bit = ((i - 1) * VisionIndex.Lattice) + (j - 1);
                if ((floor & (1UL << bit)) != 0 && !sight.Blocked(ox, oy, x, b.MinY + (j / 8.0 * b.Height)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double NearestDistanceSq(MapBounds b, double x, double y)
    {
        double nx = Math.Clamp(x, b.MinX, b.MaxX);
        double ny = Math.Clamp(y, b.MinY, b.MaxY);
        double dx = x - nx, dy = y - ny;
        return (dx * dx) + (dy * dy);
    }
}
