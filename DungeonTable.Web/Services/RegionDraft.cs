using System.Collections.Generic;
using DungeonTable.Core.Maps;

namespace DungeonTable.Web.Services;

/// <summary>
/// A region outline the Room Editor is drawing one corner at a time, for a room no rectangle fits: a
/// cave, or a wall that runs on the diagonal. Clicking the first corner again closes the outline, and
/// so does clicking the last one again, which is what a double-click does. Belongs to one editor
/// circuit, so it is not thread-safe.
/// </summary>
public sealed class RegionDraft
{
    // Below this many square world units an outline encloses nothing: its corners lie on one line.
    private const double MinimumArea = 1e-6;

    private readonly List<MapPoint> corners = new List<MapPoint>();

    /// <summary>The corners placed so far, in drawing order.</summary>
    public IReadOnlyList<MapPoint> Corners => corners;

    /// <summary>True when no corner has been placed.</summary>
    public bool IsEmpty => corners.Count == 0;

    /// <summary>True when the outline has at least three corners and they enclose something.</summary>
    public bool CanClose => corners.Count >= 3 && Math.Abs(SignedArea(corners)) > MinimumArea;

    /// <summary>
    /// Takes a click: the outline's next corner, or the signal to close it when the click lands on
    /// the first corner or repeats the last one.
    /// </summary>
    /// <param name="point">The clicked point, already snapped if snapping is on.</param>
    /// <param name="tolerance">
    /// How near a corner, in world units, a click counts as a click on that corner.
    /// </param>
    /// <returns>What the click did.</returns>
    public RegionDraftStep Add(MapPoint point, double tolerance)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            return RegionDraftStep.Ignored;
        }

        if (corners.Count > 0 && (Near(point, corners[0], tolerance) || Near(point, corners[^1], tolerance)))
        {
            // A repeated corner adds nothing: it either finishes the outline or is ignored until the
            // outline has something to enclose.
            return CanClose ? RegionDraftStep.Closed : RegionDraftStep.Ignored;
        }

        corners.Add(point);
        return RegionDraftStep.Added;
    }

    /// <summary>Removes the last corner placed, if any.</summary>
    public void Undo()
    {
        if (corners.Count > 0)
        {
            corners.RemoveAt(corners.Count - 1);
        }
    }

    /// <summary>Discards the outline.</summary>
    public void Clear() => corners.Clear();

    /// <summary>
    /// Takes the finished outline, leaving the draft empty for the next one. An outline that cannot
    /// close yet is left as it is.
    /// </summary>
    /// <returns>The outline's corners, or an empty list when it cannot close yet.</returns>
    public IReadOnlyList<MapPoint> Close()
    {
        if (!CanClose)
        {
            return Array.Empty<MapPoint>();
        }

        MapPoint[] outline = corners.ToArray();
        corners.Clear();
        return outline;
    }

    private static bool Near(MapPoint a, MapPoint b, double tolerance)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy) <= tolerance * tolerance;
    }

    // The shoelace formula: zero for corners that all lie on one line.
    private static double SignedArea(List<MapPoint> points)
    {
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            MapPoint a = points[i];
            MapPoint b = points[(i + 1) % points.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum / 2;
    }
}
