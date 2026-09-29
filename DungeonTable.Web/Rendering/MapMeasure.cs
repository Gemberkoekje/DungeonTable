using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// Turns two world-unit points into a table-ready distance. The single place the 5-ft square lives,
/// shared by the map's measuring tool and the fog lifter's vision range — two hand-kept copies of a
/// scale constant is how one of them ends up wrong.
/// </summary>
public static class MapMeasure
{
    /// <summary>How many feet one grid square is worth. The dungeon is drawn on a 5-ft grid.</summary>
    public const double FeetPerCell = 5.0;

    /// <summary>
    /// Measures between two world points. Distance in squares is Chebyshev — the count of squares
    /// crossed when a diagonal step costs the same as an orthogonal one, which is 5e's default rule —
    /// taken between the <em>cells containing</em> the points rather than from the raw delta, so the
    /// answer matches what a DM counting squares on the drawn grid would get.
    /// </summary>
    /// <param name="grid">The map's calibrated grid.</param>
    /// <param name="fromX">Start X in world units.</param>
    /// <param name="fromY">Start Y in world units.</param>
    /// <param name="toX">End X in world units.</param>
    /// <param name="toY">End Y in world units.</param>
    /// <returns>The measurement, or <see cref="MapMeasurement.None"/> for non-finite input.</returns>
    public static MapMeasurement Between(Grid grid, double fromX, double fromY, double toX, double toY)
    {
        if (!double.IsFinite(fromX) || !double.IsFinite(fromY) || !double.IsFinite(toX) || !double.IsFinite(toY))
        {
            return MapMeasurement.None;
        }

        var from = new MapPoint(fromX, fromY);
        var to = new MapPoint(toX, toY);
        double cell = grid.CellSizePx;
        if (cell <= 0)
        {
            // An uncalibrated map has no scale to report in. Say so rather than pick one: a made-up
            // "30 ft" read off a map with no grid is worse than no number at all.
            return new MapMeasurement { IsSet = true, HasGrid = false, From = from, To = to };
        }

        (int fromCol, int fromRow) = GridCells.CellAt(grid, fromX, fromY);
        (int toCol, int toRow) = GridCells.CellAt(grid, toX, toY);
        int squares = Math.Max(Math.Abs(toCol - fromCol), Math.Abs(toRow - fromRow));

        double worldDistance = Math.Sqrt(((toX - fromX) * (toX - fromX)) + ((toY - fromY) * (toY - fromY)));

        return new MapMeasurement
        {
            IsSet = true,
            HasGrid = true,
            From = from,
            To = to,
            Squares = squares,
            Feet = (int)(squares * FeetPerCell),
            StraightFeet = (int)Math.Round(worldDistance / cell * FeetPerCell, MidpointRounding.AwayFromZero),
        };
    }
}
