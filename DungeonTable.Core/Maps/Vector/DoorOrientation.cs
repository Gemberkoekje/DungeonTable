namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// Which way a door sits in its wall, derived from the door leaf's bounding-box aspect
/// (the source file has no explicit facing field).
/// </summary>
public enum DoorOrientation
{
    /// <summary>Unknown / not derivable.</summary>
    None = 0,

    /// <summary>Door spans horizontally (set into a horizontal wall).</summary>
    Horizontal,

    /// <summary>Door spans vertically (set into a vertical wall).</summary>
    Vertical,
}
