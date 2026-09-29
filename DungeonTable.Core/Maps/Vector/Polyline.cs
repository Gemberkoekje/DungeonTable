namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// An open stroke (not closed): internal wall lines, door jamb stubs, or stair steps.
/// Point counts vary — do not assume a fixed length.
/// </summary>
public sealed class Polyline
{
    /// <summary>Ordered points of the open stroke, in world units.</summary>
    public IReadOnlyList<MapPoint> Points { get; init; } = Array.Empty<MapPoint>();
}
