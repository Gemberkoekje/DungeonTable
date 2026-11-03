namespace DungeonTable.Web.Services;

/// <summary>
/// The tabs of the dock beside the DM's map. The map never leaves the screen; these switch only what
/// sits beside it. The shell keeps every tab mounted and hides the inactive ones with CSS, so
/// switching never loses a tab's scroll position, a half-typed box or the fight's open pane.
/// </summary>
public enum DockTab
{
    /// <summary>No tab (unset).</summary>
    None,

    /// <summary>The room the DM is looking at, in reading order.</summary>
    Room,

    /// <summary>The fight: initiative order and combatant tracking.</summary>
    Battle,

    /// <summary>The floor, the quests, the party, the people, the story, tonight's prep, the rules and search.</summary>
    Campaign,

    /// <summary>The pictures pushed onto the projector: catalogue, tray and uploads.</summary>
    Art,
}
