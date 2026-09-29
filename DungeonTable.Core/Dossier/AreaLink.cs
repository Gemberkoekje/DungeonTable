namespace DungeonTable.Core.Dossier;

/// <summary>
/// One authored area a reference card's subject appears in, offered as a jump so the card is a
/// signpost rather than a dead end.
/// </summary>
/// <remarks>
/// The <see cref="Relation"/> is carried because it is what makes a choice between several areas
/// meaningful: an area that lists someone among its creatures and one whose prose only mentions them
/// send the DM to very different places. The card shows every area and lets the DM pick.
/// </remarks>
public sealed class AreaLink
{
    /// <summary>Node id of the area ("data_dossiers_level_1_area_2").</summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>That area's label ("Goblin Den (Level 1, Area 2)"), for the link text.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// How the area names its subject, comma-joined where it does so in several ways ("in the room",
    /// "mentioned", "in the room, mentioned"). Empty when unknown.
    /// </summary>
    public string Relation { get; init; } = string.Empty;
}
