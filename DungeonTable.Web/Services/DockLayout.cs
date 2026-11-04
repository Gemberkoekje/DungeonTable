namespace DungeonTable.Web.Services;

/// <summary>
/// How the DM screen shares its width between the map and the dock. Each layout keeps its own dock
/// width and its own pan and zoom on the map, so going from the room to the fight and back puts both
/// where the DM left them.
/// </summary>
public enum DockLayout
{
    /// <summary>No layout (unset).</summary>
    None,

    /// <summary>
    /// The Room, Campaign and Art tabs: a reading-width dock, and the map large beside it, because the
    /// map is what the DM works on while the party explores.
    /// </summary>
    Reading,

    /// <summary>
    /// The Battle tab: a wide dock, so the initiative order and a creature's detail pane both fit side by
    /// side, and the map narrow beside it, a bird's-eye view of the fight's room and its surroundings.
    /// In a fight the map stays still and the stat blocks are what the DM reads.
    /// </summary>
    Battle,
}
