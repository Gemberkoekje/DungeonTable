using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// What the fog lifter needs to know about a map that does not change while the map is loaded, worked
/// out once per map instead of on every lift: which of each cell's sample points lie on the floor, and
/// the map's wall segments with how many of them meet at each end. One index is kept per
/// <see cref="VectorMap"/> instance, so a map read again from disk gets a new one.
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
        if (Inside((b.MinX + b.MaxX) / 2, lines[Lattice]))
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

    // The eight horizontal lines a row's sample points sit on (the lattice's seven, then the centre), each
    // as the sorted crossings of the floor's edges with it. A cell's MinY and height depend only on its row,
    // so every cell in the row shares these lines.
    private double[][] BuildRow(int row)
    {
        MapBounds b = GridCells.CellBounds(grid, 0, row);
        var ys = new double[Lattice + 1];
        for (int j = 1; j <= Lattice; j++)
        {
            ys[j - 1] = b.MinY + (j / 8.0 * b.Height);
        }

        ys[Lattice] = (b.MinY + b.MaxY) / 2;

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

    private static int RowOf(double y, Grid grid)
    {
        double row = Math.Floor((y - grid.OriginY) / grid.CellSizePx);
        return (int)Math.Clamp(row, -1, grid.Rows);
    }

    private readonly record struct FloorEdge(double AX, double AY, double BX, double BY);
}
