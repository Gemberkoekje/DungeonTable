namespace DungeonTable.Core.Maps;

/// <summary>
/// A keyed area drawn on a map as a polygon in world units, optionally linked to the authored area
/// it shows. Clicking a region on the DM view reveals it to players.
/// </summary>
public sealed class Region
{
    /// <summary>Stable identifier of the region within its map.</summary>
    public string RegionId { get; init; } = string.Empty;

    /// <summary>Human-readable label, shown when the region has no linked briefing.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Polygon vertices in map world units.</summary>
    public IReadOnlyList<MapPoint> Polygon { get; init; } = Array.Empty<MapPoint>();

    /// <summary>
    /// Linked area node id, or an empty string when the region has no briefing link.
    /// </summary>
    public string GraphNodeId { get; init; } = string.Empty;
}
