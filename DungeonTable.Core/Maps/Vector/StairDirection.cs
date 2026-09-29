namespace DungeonTable.Core.Maps.Vector;

/// <summary>
/// Ascent direction of a stair. The source file encodes no direction, so the reader leaves
/// this <see cref="None"/> rather than guessing; the editor can set it.
/// </summary>
public enum StairDirection
{
    /// <summary>Unknown / not set.</summary>
    None = 0,

    /// <summary>Ascends.</summary>
    Up,

    /// <summary>Descends.</summary>
    Down,
}
