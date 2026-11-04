namespace DungeonTable.Web.Services;

/// <summary>
/// Decides what the DM's map does when the dock moves between the reading layout and the battle
/// layout. One per DM screen, because the pan and zoom it reasons about live in that browser.
/// </summary>
/// <remarks>
/// Each layout keeps its own view. Going to the fight, the map frames the fight's room with a few cells
/// around it, the first time for that room: a bird's-eye reminder of the doors, pillars and markers the
/// party might use, in the narrow strip the battle layout leaves the map. After that the battle view is
/// the DM's own: if they moved it, going to read a room and coming back finds it where they left it.
/// A fight in another room is framed afresh. Going back to reading brings back the view the DM had
/// before the fight.
/// </remarks>
public sealed class MapViewMemory
{
    // The fight's room the battle view was last framed on. A different room frames again.
    private string framedArea = string.Empty;

    /// <summary>The layout a dock tab uses: Battle has its own, the reading tabs share one.</summary>
    /// <param name="tab">The dock tab.</param>
    /// <returns>The tab's layout.</returns>
    public static DockLayout LayoutOf(DockTab tab) => tab == DockTab.Battle ? DockLayout.Battle : DockLayout.Reading;

    /// <summary>Works out what the map does as the dock goes from one tab to another.</summary>
    /// <param name="from">The tab being left.</param>
    /// <param name="to">The tab being opened.</param>
    /// <param name="fightArea">The node id of the running fight's room; empty when no fight is running.</param>
    /// <param name="fightAreaOnMap">True when that room has a region on the map on show, so there is something to frame.</param>
    /// <returns>What to keep, what to bring back, and whether to frame the fight.</returns>
    public MapViewChange Change(DockTab from, DockTab to, string fightArea, bool fightAreaOnMap)
    {
        DockLayout leaving = LayoutOf(from);
        DockLayout opening = LayoutOf(to);
        if (leaving == opening)
        {
            return MapViewChange.None;
        }

        if (opening == DockLayout.Battle
            && fightAreaOnMap
            && fightArea.Length > 0
            && !string.Equals(fightArea, framedArea, StringComparison.Ordinal))
        {
            framedArea = fightArea;
            return new MapViewChange(leaving, DockLayout.None, true);
        }

        return new MapViewChange(leaving, opening, false);
    }
}
