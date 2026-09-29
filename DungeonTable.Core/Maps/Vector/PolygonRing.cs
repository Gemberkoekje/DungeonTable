namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// One closed ring of a polygon: the outer boundary, or a hole. Vertices come from the
/// source already closed (the first vertex is repeated as the last), so callers must
/// <b>not</b> re-close the ring.
/// </summary>
public sealed class PolygonRing
{
    /// <summary>Ring vertices in world units; first equals last (explicitly closed).</summary>
    public IReadOnlyList<MapPoint> Vertices { get; init; } = Array.Empty<MapPoint>();
}
