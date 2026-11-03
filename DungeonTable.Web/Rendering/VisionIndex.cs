using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// What the fog lifter and the projector's corridor peek need to know about a map that does not change
/// while the map is loaded, worked out once per map instead of on every lift: which of each cell's sample
/// points lie on the floor, and the map's wall segments, with how many of them meet at each end and which
/// cells each one passes. One index is kept per <see cref="VectorMap"/> instance, so a map read again
/// from disk gets a new one.
/// </summary>
/// <remarks>
/// The lifter used to test every sample point against every floor outline on the map, which is what made
/// a long lift take seconds (see <c>docs/architecture.md</c>, "The fog lifter"). The floor test here gives
/// exactly the answer <see cref="CorridorReveal.PointInRings"/> gives, from the same arithmetic: for one
/// horizontal line of sample points, the crossings of the floor's edges with that line are worked out
/// once and sorted, and a point is on the floor when an odd number of them lie to its right. The lines
/// are worked out a grid row at a time, the first time a lift reaches that row.
/// </remarks>
internal sealed class VisionIndex
{
    /// <summary>The bit of a cell's floor mask that stands for the cell's centre.</summary>
    internal const int CentreBit = 49;

    /// <summary>The sample lattice is this many points a side, at eighths of the cell (1/8 .. 7/8).</summary>
    internal const int Lattice = 7;

    // The rows' lines of sample points: the lattice's seven, then the centre as the lifter works it out
    // (halfway between the cell's edges), then the centre as the corridor peek works it out (the origin
    // plus half a cell more than the cell's index). The two centres can differ in the last bit.
    private const int CentreLine = Lattice;
    private const int CorridorLine = Lattice + 1;

    // A wall whose bounding box covers more cells than this is checked by every corridor step instead of
    // being filed under each cell; drawn walls are short, so this is for a malformed map.
    private const int MaxCellsPerWall = 4096;

    // Above this many rows the edges are not sorted into rows: a grid that large is a malformed map, and
    // scanning every edge is still correct.
    private const int MaxIndexedRows = 100_000;

    private static readonly ConditionalWeakTable<VectorMap, VisionIndex> Indexes = new();

    private readonly Grid grid;
    private readonly FloorEdge[] edges;
    private readonly int[][] edgesByRow;
    private readonly ConcurrentDictionary<int, double[][]> crossingsByRow = new();
    private readonly Func<int, double[][]> buildRow;
    private readonly Dictionary<(long, long), int> endCounts = new();
    private readonly Lazy<WallCells> wallCells;

    private VisionIndex(VectorMap map)
    {
        grid = map.Grid;
        List<IReadOnlyList<MapPoint>> rings = CorridorReveal.CollectRings(map, LayerKind.Floor);
        HasFloor = rings.Count > 0;
        edges = CollectEdges(rings);
        edgesByRow = SortIntoRows(edges, grid);
        buildRow = BuildRow;

        Walls = CorridorReveal.CollectWallSegments(map);
        foreach (CorridorReveal.Segment wall in Walls)
        {
            Count(endCounts, wall.X0, wall.Y0);
            Count(endCounts, wall.X1, wall.Y1);
        }

        wallCells = new Lazy<WallCells>(() => FileWallsByCell(Walls, grid));
    }

    /// <summary>True when the map has a floor at all; without one nothing can be seen.</summary>
    internal bool HasFloor { get; }

    /// <summary>The map's wall segments, as drawn, before any corner is extended.</summary>
    internal IReadOnlyList<CorridorReveal.Segment> Walls { get; }

    /// <summary>The index for a map, built the first time it is asked for.</summary>
    /// <param name="map">The map.</param>
    /// <returns>The map's index.</returns>
    internal static VisionIndex For(VectorMap map) => Indexes.GetValue(map, static m => new VisionIndex(m));

    /// <summary>The key two segment ends share when they meet: the end rounded to whole units.</summary>
    /// <param name="x">X of the end.</param>
    /// <param name="y">Y of the end.</param>
    /// <returns>The key.</returns>
    internal static (long, long) EndKey(double x, double y) => ((long)Math.Round(x), (long)Math.Round(y));

    /// <summary>Adds one segment end to a tally of ends per key.</summary>
    /// <param name="counts">The tally.</param>
    /// <param name="x">X of the end.</param>
    /// <param name="y">Y of the end.</param>
    internal static void Count(Dictionary<(long, long), int> counts, double x, double y)
    {
        (long, long) key = EndKey(x, y);
        counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
    }

    /// <summary>How many of the map's wall segments end at a key.</summary>
    /// <param name="key">The key (see <see cref="EndKey"/>).</param>
    /// <returns>The number of wall ends there.</returns>
    internal int WallEndsAt((long, long) key) => endCounts.TryGetValue(key, out int n) ? n : 0;

    /// <summary>
    /// Which of a cell's sample points lie on the floor: bit <c>(i - 1) * 7 + (j - 1)</c> for the lattice
    /// point at <c>i</c> eighths across and <c>j</c> eighths down, and <see cref="CentreBit"/> for the centre.
    /// </summary>
    /// <param name="col">Grid column (any column, inside the grid or not).</param>
    /// <param name="row">Grid row (any row).</param>
    /// <returns>The floor mask; zero when no sample point of the cell is floor.</returns>
    internal ulong FloorMask(int col, int row)
    {
        double[][] lines = crossingsByRow.GetOrAdd(row, buildRow);
        MapBounds b = GridCells.CellBounds(grid, col, row);
        ulong mask = 0;
        if (Inside((b.MinX + b.MaxX) / 2, lines[CentreLine]))
        {
            mask |= 1UL << CentreBit;
        }

        for (int i = 1; i <= Lattice; i++)
        {
            double x = b.MinX + (i / 8.0 * b.Width);
            for (int j = 1; j <= Lattice; j++)
            {
                if (Inside(x, lines[j - 1]))
                {
                    mask |= 1UL << (((i - 1) * Lattice) + (j - 1));
                }
            }
        }

        return mask;
    }

    /// <summary>
    /// Whether a cell's centre lies on the floor, with the centre worked out as the corridor peek does
    /// (<c>origin + (index + 0.5) * size</c>): exactly <see cref="CorridorReveal"/>'s own floor test.
    /// </summary>
    /// <param name="col">Grid column (any column).</param>
    /// <param name="row">Grid row (any row).</param>
    /// <returns>True when the centre is on the floor.</returns>
    internal bool CorridorCentreIsFloor(int col, int row)
    {
        double[][] lines = crossingsByRow.GetOrAdd(row, buildRow);
        return Inside(grid.OriginX + ((col + 0.5) * grid.CellSizePx), lines[CorridorLine]);
    }

    /// <summary>
    /// The map's walls that could cross a straight line between the centres of two neighbouring cells: the
    /// walls that pass either cell, and any wall too large to file by cell. A wall that crosses such a line
    /// meets it inside one of the two cells, so no other wall can.
    /// </summary>
    /// <param name="a">One cell.</param>
    /// <param name="b">Its neighbour.</param>
    /// <returns>The candidate walls, possibly with repeats.</returns>
    internal IEnumerable<CorridorReveal.Segment> WallsNear((int Col, int Row) a, (int Col, int Row) b)
    {
        WallCells cells = wallCells.Value;
        foreach (int index in cells.Large)
        {
            yield return Walls[index];
        }

        foreach ((int Col, int Row) cell in new[] { a, b })
        {
            if (cells.ByCell.TryGetValue(cell, out List<int> filed))
            {
                foreach (int index in filed)
                {
                    yield return Walls[index];
                }
            }
        }
    }

    // PointInRings counts the edges whose crossing with the point's horizontal line lies to the right of
    // the point (x < crossing); an odd count is inside. The crossings are sorted, so that count is the
    // number of crossings after the last one at or left of x.
    private static bool Inside(double x, double[] crossings)
    {
        if (double.IsNaN(x))
        {
            return false;
        }

        int lo = 0, hi = crossings.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (crossings[mid] > x)
            {
                hi = mid;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return ((crossings.Length - lo) & 1) == 1;
    }

    // The nine horizontal lines a row's sample points sit on (the lattice's seven, then the two centres),
    // each as the sorted crossings of the floor's edges with it. A cell's MinY and height depend only on
    // its row, so every cell in the row shares these lines.
    private double[][] BuildRow(int row)
    {
        MapBounds b = GridCells.CellBounds(grid, 0, row);
        var ys = new double[CorridorLine + 1];
        for (int j = 1; j <= Lattice; j++)
        {
            ys[j - 1] = b.MinY + (j / 8.0 * b.Height);
        }

        ys[CentreLine] = (b.MinY + b.MaxY) / 2;
        ys[CorridorLine] = grid.OriginY + ((row + 0.5) * grid.CellSizePx);

        int[] candidates = row >= 0 && row < edgesByRow.Length ? edgesByRow[row] : null;
        var lines = new double[ys.Length][];
        var found = new List<double>();
        for (int k = 0; k < ys.Length; k++)
        {
            found.Clear();
            AddCrossings(found, ys[k], candidates);
            double[] line = found.ToArray();
            Array.Sort(line);
            lines[k] = line;
        }

        return lines;
    }

    // The same test and the same arithmetic as PointInRings, edge by edge, so a crossing lands on exactly
    // the same double. A NaN crossing never counts there (x < NaN is false), so it is left out here.
    private void AddCrossings(List<double> found, double y, int[] candidates)
    {
        if (candidates is null)
        {
            foreach (FloorEdge e in edges)
            {
                AddCrossing(found, e, y);
            }

            return;
        }

        foreach (int index in candidates)
        {
            AddCrossing(found, edges[index], y);
        }
    }

    private static void AddCrossing(List<double> found, FloorEdge e, double y)
    {
        if ((e.AY > y) != (e.BY > y))
        {
            double crossing = ((e.BX - e.AX) * (y - e.AY) / (e.BY - e.AY)) + e.AX;
            if (!double.IsNaN(crossing))
            {
                found.Add(crossing);
            }
        }
    }

    // Every ring's edges in PointInRings' order and orientation: a is vertex i, b the vertex before it.
    private static FloorEdge[] CollectEdges(List<IReadOnlyList<MapPoint>> rings)
    {
        var list = new List<FloorEdge>();
        foreach (IReadOnlyList<MapPoint> ring in rings)
        {
            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                list.Add(new FloorEdge(ring[i].X, ring[i].Y, ring[j].X, ring[j].Y));
            }
        }

        return list.ToArray();
    }

    // Each edge filed under the grid rows its height spans, one row of slack either side, so building a row
    // looks only at the edges that can cross it. Without a usable grid every row scans every edge instead.
    private static int[][] SortIntoRows(FloorEdge[] edges, Grid grid)
    {
        if (grid.Rows <= 0 || grid.Rows > MaxIndexedRows || grid.CellSizePx <= 0)
        {
            return Array.Empty<int[]>();
        }

        var rows = new List<int>[grid.Rows];
        for (int index = 0; index < edges.Length; index++)
        {
            FloorEdge e = edges[index];
            if (!double.IsFinite(e.AY) || !double.IsFinite(e.BY))
            {
                continue;
            }

            int first = RowOf(Math.Min(e.AY, e.BY), grid) - 1;
            int last = RowOf(Math.Max(e.AY, e.BY), grid) + 1;
            for (int row = Math.Max(first, 0); row <= Math.Min(last, grid.Rows - 1); row++)
            {
                (rows[row] ??= new List<int>()).Add(index);
            }
        }

        var result = new int[grid.Rows][];
        for (int row = 0; row < rows.Length; row++)
        {
            result[row] = rows[row]?.ToArray() ?? Array.Empty<int>();
        }

        return result;
    }

    // Each wall filed under every cell its bounding box touches, with a millionth of a cell of slack so a
    // wall lying along a cell edge is filed on both sides of it.
    private static WallCells FileWallsByCell(IReadOnlyList<CorridorReveal.Segment> walls, Grid grid)
    {
        var filed = new WallCells();
        double size = grid.CellSizePx;
        if (size <= 0)
        {
            filed.Large.AddRange(Enumerable.Range(0, walls.Count));
            return filed;
        }

        double slack = size * 1e-6;
        for (int index = 0; index < walls.Count; index++)
        {
            CorridorReveal.Segment w = walls[index];
            double col0 = Math.Floor((Math.Min(w.X0, w.X1) - slack - grid.OriginX) / size);
            double col1 = Math.Floor((Math.Max(w.X0, w.X1) + slack - grid.OriginX) / size);
            double row0 = Math.Floor((Math.Min(w.Y0, w.Y1) - slack - grid.OriginY) / size);
            double row1 = Math.Floor((Math.Max(w.Y0, w.Y1) + slack - grid.OriginY) / size);
            if (!FitsInCells(col0) || !FitsInCells(col1) || !FitsInCells(row0) || !FitsInCells(row1)
                || ((col1 - col0 + 1) * (row1 - row0 + 1)) > MaxCellsPerWall)
            {
                filed.Large.Add(index);
                continue;
            }

            for (int row = (int)row0; row <= (int)row1; row++)
            {
                for (int col = (int)col0; col <= (int)col1; col++)
                {
                    if (!filed.ByCell.TryGetValue((col, row), out List<int> list))
                    {
                        list = new List<int>();
                        filed.ByCell[(col, row)] = list;
                    }

                    list.Add(index);
                }
            }
        }

        return filed;
    }

    // A cell index that is a number and small enough to count with an int.
    private static bool FitsInCells(double index) => Math.Abs(index) < 1e8;

    private static int RowOf(double y, Grid grid)
    {
        double row = Math.Floor((y - grid.OriginY) / grid.CellSizePx);
        return (int)Math.Clamp(row, -1, grid.Rows);
    }

    private readonly record struct FloorEdge(double AX, double AY, double BX, double BY);

    // The walls by the cells they pass, and the few too large to file that way.
    private sealed class WallCells
    {
        internal Dictionary<(int Col, int Row), List<int>> ByCell { get; } = new();

        internal List<int> Large { get; } = new();
    }
}
