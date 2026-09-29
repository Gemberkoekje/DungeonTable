namespace DungeonTable.Web.Services;

/// <summary>
/// The Map tab's active tool. Exactly one is active at a time; it decides what a click on the map
/// canvas does. The declared order is the order of the tool rail and of its <c>1</c>–<c>8</c>
/// keyboard shortcuts.
/// </summary>
public enum MapTool
{
    /// <summary>No tool (unset).</summary>
    None,

    /// <summary>Click a region to load its briefing; nothing is shown to the players.</summary>
    Inspect,

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
