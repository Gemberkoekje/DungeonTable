namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A resolved Dungeon Scrawl geometry blob: the vertices a layer or feature draws. Either
/// collection may be empty (stairs, for instance, carry no polygons; three stair blobs in
/// the sample map are entirely empty), so guard both before use.
/// </summary>
public sealed class VectorGeometry
{
    /// <summary>A shared empty geometry, used as a safe default.</summary>
    public static VectorGeometry Empty { get; } = new VectorGeometry();

    /// <summary>Filled shapes (with optional holes). May be empty.</summary>
    public IReadOnlyList<MapPolygon> Polygons { get; init; } = Array.Empty<MapPolygon>();

    /// <summary>Open strokes. May be empty.</summary>
    public IReadOnlyList<Polyline> Polylines { get; init; } = Array.Empty<Polyline>();

    /// <summary>True when neither polygons nor polylines are present.</summary>
    public bool IsEmpty => Polygons.Count == 0 && Polylines.Count == 0;
}
