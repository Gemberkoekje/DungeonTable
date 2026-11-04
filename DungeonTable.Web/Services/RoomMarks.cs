using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Web.Rendering;

namespace DungeonTable.Web.Services;

/// <summary>
/// What the Room Editor marked in a room on the map, rather than what its dossier says: the traps and
/// hazards, and the secrets (a secret or hidden-door marker, a secret door in its walls). The badges
/// beside the room's label and its peek card read both.
/// </summary>
public sealed class RoomMarks
{
    /// <summary>A room the map marks nothing in, or one that has no region on the map on show.</summary>
    public static RoomMarks None { get; } = new RoomMarks();

    /// <summary>The traps and hazards in the room, each as a line ("Trap", "Hazard: loose stones").</summary>
    public IReadOnlyList<string> Hazards { get; init; } = Array.Empty<string>();

    /// <summary>The secrets the map marks in the room ("Secret door", "Hidden door: behind the altar").</summary>
    public IReadOnlyList<string> Secrets { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Finds what the map marks in a room. A marker counts when it is linked to the room or stands in one
    /// of its regions. A secret door counts when it is in the room's walls: its middle is inside a region,
    /// or within a square of one's edge, which is where a door drawn on the wall line sits. A door between
    /// two rooms is a secret of both.
    /// </summary>
    /// <param name="areaNode">The room's node id.</param>
    /// <param name="regions">The room's regions on the map on show.</param>
    /// <param name="markers">Every marker on the map.</param>
    /// <param name="secretDoors">Where each secret door on the map is.</param>
    /// <param name="cellSize">One square, in world units.</param>
    /// <returns>What the map marks there; <see cref="None"/> when the room has no region and no marker is linked to it.</returns>
    public static RoomMarks Within(
        string areaNode,
        IReadOnlyList<Region> regions,
        IReadOnlyList<FeatureMarker> markers,
        IReadOnlyList<MapBounds> secretDoors,
        double cellSize)
    {
        Region[] shapes = (regions ?? Array.Empty<Region>()).Where(region => region is not null && region.Polygon.Count >= 3).ToArray();
        var hazards = new List<string>();
        var secrets = new List<string>();

        foreach (FeatureMarker marker in markers ?? Array.Empty<FeatureMarker>())
        {
            if (marker is null || !(IsLinked(marker, areaNode) || shapes.Any(region => Inside(region, marker.Position.X, marker.Position.Y))))
            {
                continue;
            }

            switch (marker.Kind)
            {
                case FeatureKind.Trap:
                    hazards.Add(Line("Trap", marker.Label));
                    break;
                case FeatureKind.Hazard:
                    hazards.Add(Line("Hazard", marker.Label));
                    break;
                case FeatureKind.Secret:
                    secrets.Add(Line("Secret", marker.Label));
                    break;
                case FeatureKind.HiddenDoor:
                    secrets.Add(Line("Hidden door", marker.Label));
                    break;
                default:
                    break;
            }
        }

        double reach = cellSize > 0 ? cellSize : 1;
        int doors = (secretDoors ?? Array.Empty<MapBounds>())
            .Count(door => shapes.Any(region => InWalls(region, door, reach)));
        if (doors > 0)
        {
            secrets.Insert(0, doors == 1 ? "Secret door" : doors.ToString(CultureInfo.InvariantCulture) + " secret doors");
        }

        return hazards.Count == 0 && secrets.Count == 0
            ? None
            : new RoomMarks { Hazards = hazards, Secrets = secrets };
    }

    private static bool IsLinked(FeatureMarker marker, string areaNode) =>
        !string.IsNullOrEmpty(areaNode) && string.Equals(marker.GraphNodeId, areaNode, StringComparison.Ordinal);

    private static string Line(string kind, string label) =>
        string.IsNullOrWhiteSpace(label) ? kind : kind + ": " + label.Trim();

    private static bool Inside(Region region, double x, double y) =>
        CorridorReveal.PointInRings(x, y, new[] { region.Polygon });

    private static bool InWalls(Region region, MapBounds door, double reach)
    {
        double x = door.MinX + (door.Width / 2);
        double y = door.MinY + (door.Height / 2);
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            return false;
        }

        return Inside(region, x, y) || DistanceToEdge(region.Polygon, x, y) <= reach;
    }

    private static double DistanceToEdge(IReadOnlyList<MapPoint> polygon, double x, double y)
    {
        double nearest = double.PositiveInfinity;
        for (int i = 0; i < polygon.Count; i++)
        {
            MapPoint a = polygon[i];
            MapPoint b = polygon[(i + 1) % polygon.Count];
            nearest = Math.Min(nearest, DistanceToSegment(a, b, x, y));
        }

        return nearest;
    }

    private static double DistanceToSegment(MapPoint a, MapPoint b, double x, double y)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        double t = lengthSquared > 0 ? Math.Clamp((((x - a.X) * dx) + ((y - a.Y) * dy)) / lengthSquared, 0, 1) : 0;
        double px = a.X + (t * dx) - x;
        double py = a.Y + (t * dy) - y;
        return Math.Sqrt((px * px) + (py * py));
    }
}
