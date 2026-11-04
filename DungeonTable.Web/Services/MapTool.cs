namespace DungeonTable.Web.Services;

/// <summary>
/// What a click on the DM's map does. Exactly one is active at a time. <see cref="Look"/> is the
/// default and the map goes back to it on Esc and after a one-click tool has been used (see
/// <see cref="ToolSelection"/>). The declared order is the order of the map's toolbar and of its
/// <c>1</c>–<c>8</c> keyboard shortcuts.
/// </summary>
public enum MapTool
{
    /// <summary>No tool (unset).</summary>
    None,

    /// <summary>
    /// The default: click a room to read it in the dock, click a door to open or close it. Nothing a
    /// look does is shown to the players except a door, and a secret door the players have not found
    /// is left alone.
    /// </summary>
    Look,

    /// <summary>Click a region, door or feature to show or hide it on the player projector.</summary>
    Reveal,

    /// <summary>Drag to paint (or erase) fog-reveal cells with a sized brush.</summary>
    Fog,

    /// <summary>Click a spot to reveal everything a viewer standing there could see.</summary>
    Lift,

    /// <summary>Click a door to open, close, discover or re-hide it.</summary>
    Doors,

    /// <summary>Click a concealed stair or decoration to show it to the players.</summary>
    Conceal,

    /// <summary>Drag between two points to read off the distance; the DM's map only.</summary>
    Measure,

    /// <summary>Click a spot to point at it on the player projector.</summary>
    Ping,
}
