using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// What the party sees as the DM walks its token across the map: the old lifter's rule
/// (<see cref="VisionReveal"/>), applied from every square the party passes through rather than from
/// one clicked spot.
/// </summary>
/// <remarks>
/// A drag reports where the pointer is about once a frame, so a quick drag jumps several squares
/// between two reports. Lifting only where the reports land would leave gaps, a side passage walked
/// past unseen, so the walk is filled in square by square (<see cref="Path"/>), as the fog brush fills
/// in a quick stroke. A square with no floor in it (solid rock the DM dragged across) is not stood in:
/// a viewer in the middle of a wall could see into both rooms it divides.
/// </remarks>
public static class PartyVision
{
    // How finely a walk is sampled, in squares. A quarter square cannot step over a square the walk
    // crosses except at its very corner, where a square of floor touched only at a point shows nothing
    // the squares on either side do not.
    private const double Step = 0.25;

    /// <summary>
    /// The squares a straight walk passes through, in order, from the square it starts in to the square
    /// it ends in, each once. Empty on a map with no calibrated grid, which has no squares.
    /// </summary>
    /// <param name="grid">The map's grid.</param>
    /// <param name="from">Where the walk starts, in world units.</param>
    /// <param name="to">Where the walk ends, in world units.</param>
    /// <returns>The squares as (column, row) grid coordinates.</returns>
    public static IReadOnlyList<(int Col, int Row)> Path(Grid grid, MapPoint from, MapPoint to)
    {
        double cell = grid.CellSizePx;
        if (cell <= 0)
        {
            return Array.Empty<(int, int)>();
        }

        double dx = to.X - from.X, dy = to.Y - from.Y;
        int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((dx * dx) + (dy * dy)) / (cell * Step)));

        var path = new List<(int Col, int Row)>();
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            (int Col, int Row) square = GridCells.CellAt(grid, from.X + (dx * t), from.Y + (dy * t));
            if (path.Count == 0 || path[^1] != square)
            {
                path.Add(square);
            }
        }

        return path;
    }

    /// <summary>
    /// What the party sees from each of some squares it stands in, put together: every floor tile within
    /// its vision range with a clear line from the middle of one of them. Squares with no floor are
    /// skipped (see the remarks on the class).
    /// </summary>
    /// <param name="map">The map.</param>
    /// <param name="standing">The squares the party stood in.</param>
    /// <param name="visionFeet">How far the party sees, in feet.</param>
    /// <param name="blockingDoorIds">Secret doors the players have not found: they block sight like a wall.</param>
    /// <param name="openDoorIds">Doors standing open: sight passes through them.</param>
    /// <returns>The tiles seen, each once.</returns>
    public static IReadOnlyList<(int Col, int Row)> SeenFrom(
        VectorMap map, IEnumerable<(int Col, int Row)> standing, int visionFeet,
        IReadOnlyCollection<string> blockingDoorIds, IReadOnlyCollection<string> openDoorIds)
    {
        Grid grid = map.Grid;
        if (grid.CellSizePx <= 0 || visionFeet <= 0)
        {
            return Array.Empty<(int, int)>();
        }

        VisionIndex index = VisionIndex.For(map);
        var seen = new HashSet<(int Col, int Row)>();
        var order = new List<(int Col, int Row)>();
        foreach ((int col, int row) in standing.Distinct().Where(square => index.FloorMask(square.Col, square.Row) != 0))
        {
            MapBounds square = GridCells.CellBounds(grid, col, row);
            double x = (square.MinX + square.MaxX) / 2, y = (square.MinY + square.MaxY) / 2;
            order.AddRange(VisionReveal.Find(map, x, y, visionFeet, blockingDoorIds, openDoorIds).Where(seen.Add));
        }

        return order;
    }
}
