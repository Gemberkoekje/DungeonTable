namespace DungeonTable.Core.Maps;

/// <summary>
/// Calibration of the 5-ft grid over a map, in the map's own units (world units for a vector
/// map). Enables grid-snapped drawing/fog and later measurement.
/// </summary>
public sealed class Grid
{
    /// <summary>Offset of the grid origin along X.</summary>
    public double OriginX { get; init; }

    /// <summary>Offset of the grid origin along Y.</summary>
    public double OriginY { get; init; }

    /// <summary>Size of one grid cell (world units for a vector map).</summary>
    public double CellSizePx { get; init; }

    /// <summary>Number of grid columns.</summary>
    public int Cols { get; init; }

    /// <summary>Number of grid rows.</summary>
    public int Rows { get; init; }
}
