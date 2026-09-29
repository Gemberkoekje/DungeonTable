namespace DungeonTable.Core.Briefing;

/// <summary>
/// One of the area's authored exits: a way from this room to another room or area. Powers the DM
/// panel's "what connects to this room" navigation.
/// </summary>
public sealed class ConnectionEntry
{
    /// <summary>
    /// Node id of the connected room/area. Empty for an exit that names no loaded area, either
    /// because its floor is not authored yet (<see cref="Pending"/>) or because it is broken.
    /// </summary>
    public string TargetRoomId { get; init; } = string.Empty;

    /// <summary>Human-readable label of the connected room/area; the exit as written when it names none.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Optional compass hint (e.g. "north"), filled from map region annotations. Empty when unknown.
    /// </summary>
    public string Direction { get; init; } = string.Empty;

    /// <summary>The author's note on the way through ("secret door", "stairs down"), in prose.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>True when the players do not know the way exists.</summary>
    public bool Secret { get; init; }

    /// <summary>
    /// True when the exit leads to a floor nobody has authored yet. It shows as a label, not a jump,
    /// until that floor's file is loaded; it is not an error.
    /// </summary>
    public bool Pending { get; init; }
}
