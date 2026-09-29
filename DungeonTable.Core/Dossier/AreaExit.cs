namespace DungeonTable.Core.Dossier;

/// <summary>
/// One entry of an area's authored exit list: where the party can go from here. The briefing's
/// connection chips are built from it.
/// </summary>
public sealed class AreaExit
{
    /// <summary>
    /// Where it leads, written the way an area link is: <c>area 6a</c> on this floor, or <c>L2 area 14</c>
    /// on another. An exit to a floor nobody has authored yet stays a muted label until that floor
    /// is loaded, and then becomes a jump by itself.
    /// </summary>
    public string To { get; init; } = string.Empty;

    /// <summary>How, or what is in the way ("secret door", "stairs down"), in prose.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>True when the players do not know the way exists: a secret door, a hidden passage.</summary>
    public bool Secret { get; init; }
}
