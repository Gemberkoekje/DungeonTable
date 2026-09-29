namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// An axis-aligned bounding box in Dungeon Scrawl world units. Used to derive the SVG
/// viewBox and grid calibration, and to place feature glyphs.
/// </summary>
/// <param name="MinX">Left edge.</param>
/// <param name="MinY">Top edge (Y grows downward).</param>
/// <param name="MaxX">Right edge.</param>
/// <param name="MaxY">Bottom edge.</param>
public readonly record struct MapBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    /// <summary>Width of the box (<see cref="MaxX"/> - <see cref="MinX"/>).</summary>
    public double Width => MaxX - MinX;

    /// <summary>Height of the box (<see cref="MaxY"/> - <see cref="MinY"/>).</summary>
    public double Height => MaxY - MinY;

    /// <summary>True when the box has no extent (an empty or unset geometry).</summary>
    public bool IsEmpty => Width <= 0 && Height <= 0;
}
