namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// A polygon with optional holes. <see cref="Rings"/>[0] is the outer boundary and any
/// further rings are holes; render with an even-odd / non-zero fill rule rather than
/// treating each ring as a separate shape.
/// </summary>
public sealed class MapPolygon
{
    /// <summary>Outer ring followed by hole rings.</summary>
    public IReadOnlyList<PolygonRing> Rings { get; init; } = Array.Empty<PolygonRing>();
}
