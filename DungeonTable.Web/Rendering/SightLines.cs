using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;

namespace DungeonTable.Web.Rendering;

/// <summary>
/// The walls one lift has to test its sight lines against, filed in a grid of buckets around the viewer
/// so that a sight line tests only the walls in the buckets it passes through, nearest first. Built once
/// per lift: the walls it holds depend on which doors are secret and which are open at that moment.
/// </summary>
/// <remarks>
/// It holds exactly the walls the lifter has always used (every wall segment plus a segment across each
/// still-hidden secret door, each extended past the corners where segments meet, within reach of the
/// viewer), so a sight line is blocked here exactly when it was blocked before. The buckets only change
/// which walls are looked at, never the answer: a wall a sight line crosses always lies in a bucket the
/// line passes through.
/// </remarks>
internal sealed class SightLines
{
    // The bucket grid never grows past this many buckets a side; a longer reach gets larger buckets.
    private const int MaxBuckets = 256;

    private readonly CorridorReveal.Segment[] walls;
    private readonly List<MapBounds> openGaps;
    private readonly double originX;
    private readonly double originY;
    private readonly double size;
    private readonly double slack;
    private readonly int span;
    private readonly int[] bucketStart;
    private readonly int[] bucketWalls;

    // The sight line each wall was last tested against, so a wall filed in several buckets along one line
    // is tested once.
    private readonly int[] testedBy;
    private int line;

    // The sight line being tested, from the viewer to a sample point.
    private double fromX;
    private double fromY;
    private double toX;
    private double toY;

    private SightLines(CorridorReveal.Segment[] walls, List<MapBounds> openGaps, double minX, double minY, double extent, double cell)
    {
        this.walls = walls;
        this.openGaps = openGaps;
        originX = minX;
        originY = minY;
        size = Math.Max(cell, extent / MaxBuckets);
        slack = size * 1e-6;
        span = (int)Math.Ceiling(extent / size) + 1;
        testedBy = new int[walls.Length];
        (bucketStart, bucketWalls) = FileWalls();
    }

    /// <summary>The walls a viewer's sight lines can meet, within a square reach of the viewer.</summary>
    /// <param name="index">The map's vision index.</param>
    /// <param name="map">The map (its doors and grid).</param>
    /// <param name="viewerX">Viewer X in world units.</param>
    /// <param name="viewerY">Viewer Y in world units.</param>
    /// <param name="reach">How far from the viewer, along either axis, a sight line can end.</param>
    /// <param name="secretDoorIds">Doors still hidden; each blocks sight like a wall.</param>
    /// <param name="openDoorIds">Doors standing open; sight passes through each, wall or not.</param>
    /// <returns>The sight lines for this lift.</returns>
    internal static SightLines Around(
        VisionIndex index, VectorMap map, double viewerX, double viewerY, double reach,
        IReadOnlyCollection<string> secretDoorIds, IReadOnlyCollection<string> openDoorIds)
    {
        Grid grid = map.Grid;
        double extend = grid.CellSizePx * 0.5;

        var secret = new List<CorridorReveal.Segment>();
        CorridorReveal.AddSecretDoorSegments(secret, map, grid, secretDoorIds);
        var secretEnds = new Dictionary<(long, long), int>();
        foreach (CorridorReveal.Segment s in secret)
        {
            VisionIndex.Count(secretEnds, s.X0, s.Y0);
            VisionIndex.Count(secretEnds, s.X1, s.Y1);
        }

        double minX = viewerX - reach, minY = viewerY - reach, maxX = viewerX + reach, maxY = viewerY + reach;

        // Extending a corner lengthens a segment by at most `extend` at each end, so a wall further than
        // that outside the reach can never come within it; the exact test is the one after extending.
        double margin = (extend * 2) + 1;
        var kept = new List<CorridorReveal.Segment>();
        foreach (CorridorReveal.Segment wall in index.Walls.Where(
            w => CorridorReveal.InRegion(w, minX - margin, minY - margin, maxX + margin, maxY + margin)))
        {
            Keep(kept, ExtendAtCorners(wall, extend, index, secretEnds), minX, minY, maxX, maxY);
        }

        foreach (CorridorReveal.Segment s in secret)
        {
            Keep(kept, ExtendAtCorners(s, extend, index, secretEnds), minX, minY, maxX, maxY);
        }

        List<MapBounds> gaps = CorridorReveal.CollectOpenDoorGaps(map, openDoorIds);
        return new SightLines(kept.ToArray(), gaps, minX, minY, reach * 2, grid.CellSizePx);
    }

    /// <summary>
    /// True when a wall stands between two points: the line between them crosses a wall anywhere but in
    /// the opening of an open door.
    /// </summary>
    /// <param name="viewerX">The viewer's X.</param>
    /// <param name="viewerY">The viewer's Y.</param>
    /// <param name="sampleX">The sample point's X.</param>
    /// <param name="sampleY">The sample point's Y.</param>
    /// <returns>True when sight is blocked.</returns>
    internal bool Blocked(double viewerX, double viewerY, double sampleX, double sampleY)
    {
        NextLine();
        fromX = viewerX;
        fromY = viewerY;
        toX = sampleX;
        toY = sampleY;
        double lowX = Math.Min(fromX, toX), highX = Math.Max(fromX, toX);
        double lowY = Math.Min(fromY, toY), highY = Math.Max(fromY, toY);
        double dx = toX - fromX, dy = toY - fromY;
        bool steep = Math.Abs(dx) <= size * 1e-9;
        int firstCol = Bucket(lowX - slack - originX), lastCol = Bucket(highX + slack - originX);
        bool rightward = toX >= fromX;
        bool downward = toY >= fromY;

        // Column by column, from the viewer's end: the stretch of the line inside the column covers these
        // rows (all of them for a line too steep to slope), give or take the slack.
        for (int step = 0; step <= lastCol - firstCol; step++)
        {
            int col = rightward ? firstCol + step : lastCol - step;
            double rowLow = lowY, rowHigh = highY;
            if (!steep)
            {
                double xa = Math.Clamp(originX + (col * size), lowX, highX);
                double xb = Math.Clamp(originX + ((col + 1) * size), lowX, highX);
                double ya = fromY + ((xa - fromX) * dy / dx);
                double yb = fromY + ((xb - fromX) * dy / dx);
                rowLow = Math.Max(Math.Min(ya, yb), lowY);
                rowHigh = Math.Min(Math.Max(ya, yb), highY);
            }

            if (ColumnBlocks(col, Bucket(rowLow - slack - originY), Bucket(rowHigh + slack - originY), downward))
            {
                return true;
            }
        }

        return false;
    }

    private bool ColumnBlocks(int col, int firstRow, int lastRow, bool downward)
    {
        for (int step = 0; step <= lastRow - firstRow; step++)
        {
            int row = downward ? firstRow + step : lastRow - step;
            int bucket = (row * span) + col;
            for (int k = bucketStart[bucket]; k < bucketStart[bucket + 1]; k++)
            {
                int index = bucketWalls[k];
                if (testedBy[index] == line)
                {
                    continue;
                }

                testedBy[index] = line;
                CorridorReveal.Segment w = walls[index];
                if (CorridorReveal.SegmentIntersection(fromX, fromY, toX, toY, w.X0, w.Y0, w.X1, w.Y1, out double ix, out double iy)
                    && !InOpenGap(ix, iy))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool InOpenGap(double x, double y)
    {
        foreach (MapBounds g in openGaps)
        {
            if (x >= g.MinX && x <= g.MaxX && y >= g.MinY && y <= g.MaxY)
            {
                return true;
            }
        }

        return false;
    }

    private void NextLine()
    {
        if (line == int.MaxValue)
        {
            Array.Clear(testedBy);
            line = 0;
        }

        line++;
    }

    private int Bucket(double offset)
    {
        double index = Math.Floor(offset / size);
        return double.IsNaN(index) ? 0 : (int)Math.Clamp(index, 0, span - 1);
    }

    // Files each wall under every bucket its bounding box touches (with the slack), as one flat array
    // indexed by bucket: bucketStart[b] .. bucketStart[b + 1] are bucket b's walls.
    private (int[] Start, int[] Walls) FileWalls()
    {
        var start = new int[(span * span) + 1];
        ForEachBucket((bucket, _) => start[bucket + 1]++);
        for (int b = 0; b < span * span; b++)
        {
            start[b + 1] += start[b];
        }

        var filed = new int[start[span * span]];
        var next = (int[])start.Clone();
        ForEachBucket((bucket, wall) => filed[next[bucket]++] = wall);
        return (start, filed);
    }

    private void ForEachBucket(Action<int, int> visit)
    {
        for (int index = 0; index < walls.Length; index++)
        {
            CorridorReveal.Segment w = walls[index];
            int col0 = Bucket(Math.Min(w.X0, w.X1) - slack - originX), col1 = Bucket(Math.Max(w.X0, w.X1) + slack - originX);
            int row0 = Bucket(Math.Min(w.Y0, w.Y1) - slack - originY), row1 = Bucket(Math.Max(w.Y0, w.Y1) + slack - originY);
            for (int row = row0; row <= row1; row++)
            {
                for (int col = col0; col <= col1; col++)
                {
                    visit((row * span) + col, index);
                }
            }
        }
    }

    private static void Keep(
        List<CorridorReveal.Segment> kept, CorridorReveal.Segment wall, double minX, double minY, double maxX, double maxY)
    {
        if (CorridorReveal.InRegion(wall, minX, minY, maxX, maxY))
        {
            kept.Add(wall);
        }
    }

    // Extends a segment slightly past each CORNER, an end where two or more segments meet (the map's walls
    // and the secret doors' segments counted together), so a sight line cannot thread through the exact
    // point where two walls meet at a right angle and leak into the next room: the strict crossing test
    // treats a line touching a shared end as crossing neither wall. A free end (a doorway jamb, the end of
    // one segment only) is left alone, so open doorways still see through. Collinear joints extend harmlessly
    // along their own line.
    private static CorridorReveal.Segment ExtendAtCorners(
        CorridorReveal.Segment w, double amount, VisionIndex index, Dictionary<(long, long), int> secretEnds)
    {
        double dx = w.X1 - w.X0, dy = w.Y1 - w.Y0;
        double len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1e-6)
        {
            return w;
        }

        double ux = dx / len * amount, uy = dy / len * amount;
        double x0 = w.X0, y0 = w.Y0, x1 = w.X1, y1 = w.Y1;
        if (EndsAt(VisionIndex.EndKey(w.X0, w.Y0), index, secretEnds) >= 2)
        {
            x0 -= ux;
            y0 -= uy;
        }

        if (EndsAt(VisionIndex.EndKey(w.X1, w.Y1), index, secretEnds) >= 2)
        {
            x1 += ux;
            y1 += uy;
        }

        return new CorridorReveal.Segment(x0, y0, x1, y1);
    }

    private static int EndsAt((long, long) key, VisionIndex index, Dictionary<(long, long), int> secretEnds) =>
        index.WallEndsAt(key) + (secretEnds.TryGetValue(key, out int n) ? n : 0);
}
