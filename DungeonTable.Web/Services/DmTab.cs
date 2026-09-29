namespace DungeonTable.Web.Services;

/// <summary>
/// The DM screen's top-level tabs. The shell keeps every panel mounted and hides the inactive ones
/// with CSS, so switching tabs never resets the map's pan/zoom or the nav history.
/// </summary>
public enum DmTab
{
    /// <summary>No tab (unset).</summary>
    None,

    /// <summary>The map: tool rail, canvas, and the player-view framing box.</summary>
    Map,

    /// <summary>The briefing: area dossier, reference drawer, history and the orientation mini map.</summary>
    Info,

    /// <summary>The fight: initiative order and combatant tracking.</summary>
    Battle,

    /// <summary>The pictures pushed onto the projector: catalogue, tray and uploads.</summary>
    Art,
}
