namespace DungeonTable.Core.Maps;

/// <summary>A point in map world units (the shared coordinate space of the vector map).</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate (grows downward).</param>
public readonly record struct MapPoint(double X, double Y);
