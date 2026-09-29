using System.Collections.Generic;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// World-unit ↔ grid-cell conversions for the fog brush and the Room Editor's snap. The grid comes
/// from the map's <see cref="Grid"/> (calibrated from the .ds); the fog reveal mask is painted in
/// whole cells so its edges line up with the printed 5-ft grid. Shared by the DM (snapping a paint
/// gesture), both views (turning a painted cell back into a rectangle for the reveal mask) and the
/// editor (snapping a region's corners to the grid lines and a marker into its square).
/// </summary>
public static class GridCells
{
    /// <summary>
    /// Snaps a world point to the nearest grid intersection: where a region's corner belongs, so an
    /// outline runs along the printed grid lines. A map with no calibrated grid has nothing to snap
    /// to, and the point comes back as it was.
    /// </summary>
    /// <param name="grid">The map grid.</param>
    /// <param name="worldX">World X.</param>
    /// <param name="worldY">World Y.</param>
    /// <returns>The nearest grid intersection, or the point itself on an uncalibrated map.</returns>
    public static MapPoint NearestCorner(Grid grid, double worldX, double worldY)
    {
        double size = grid.CellSizePx;
        if (size <= 0)
        {
            return new MapPoint(worldX, worldY);
        }

        return new MapPoint(
            (Math.Round((worldX - grid.OriginX) / size) * size) + grid.OriginX,
            (Math.Round((worldY - grid.OriginY) / size) * size) + grid.OriginY);
    }

    /// <summary>
    /// The centre of the grid cell that contains a world point: where a feature marker belongs, so a
    /// trap sits in its square rather than on the corner four squares share. A map with no calibrated
    /// grid has no squares, and the point comes back as it was.
    /// </summary>
    /// <param name="grid">The map grid.</param>
    /// <param name="worldX">World X.</param>
    /// <param name="worldY">World Y.</param>
    /// <returns>The containing cell's centre, or the point itself on an uncalibrated map.</returns>
    public static MapPoint CellCentre(Grid grid, double worldX, double worldY)
    {
        double size = grid.CellSizePx;
        if (size <= 0)
        {
            return new MapPoint(worldX, worldY);
        }

        (int col, int row) = CellAt(grid, worldX, worldY);
        return new MapPoint(grid.OriginX + ((col + 0.5) * size), grid.OriginY + ((row + 0.5) * size));
    }

    /// <summary>Snaps a world point to the grid cell that contains it.</summary>
    /// <param name="grid">The map grid.</param>
    /// <param name="worldX">World X.</param>
    /// <param name="worldY">World Y.</param>
    /// <returns>The (column, row) of the containing cell.</returns>
    public static (int Col, int Row) CellAt(Grid grid, double worldX, double worldY)
    {
        double size = grid.CellSizePx > 0 ? grid.CellSizePx : 1;
        int col = (int)Math.Floor((worldX - grid.OriginX) / size);
        int row = (int)Math.Floor((worldY - grid.OriginY) / size);
        return (col, row);
    }

    /// <summary>
    /// The footprint of a fog brush stroke: the square block of <paramref name="size"/> ×
    /// <paramref name="size"/> cells around the cell containing a world point. A size of 1 is the
    /// single containing cell; larger sizes extend right and down from it (an even brush has no
    /// true centre cell, so it is anchored at the pointer's own cell).
    /// </summary>
    /// <param name="grid">The map grid.</param>
    /// <param name="worldX">Pointer X in world units.</param>
    /// <param name="worldY">Pointer Y in world units.</param>
    /// <param name="size">Brush width in cells; values below 1 are treated as 1.</param>
    /// <returns>The (column, row) of every cell the stroke covers.</returns>
    public static IReadOnlyList<(int Col, int Row)> BrushCells(Grid grid, double worldX, double worldY, int size)
    {
        (int col, int row) = CellAt(grid, worldX, worldY);
        if (size <= 1)
        {
            return new[] { (col, row) };
        }

        int back = (size - 1) / 2;
        var cells = new List<(int Col, int Row)>(size * size);
        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                cells.Add((col - back + dx, row - back + dy));
            }
        }

        return cells;
    }

    /// <summary>Returns the world-unit bounds of a grid cell.</summary>
    /// <param name="grid">The map grid.</param>
    /// <param name="col">Grid column.</param>
    /// <param name="row">Grid row.</param>
    /// <returns>The cell rectangle in world units.</returns>
    public static MapBounds CellBounds(Grid grid, int col, int row)
    {
        double size = grid.CellSizePx > 0 ? grid.CellSizePx : 1;
        double x = grid.OriginX + (col * size);
        double y = grid.OriginY + (row * size);
        return new MapBounds(x, y, x + size, y + size);
    }
}
