namespace DungeonTable.Web.Services;

/// <summary>
/// What the DM's map does with its pan and zoom when the dock changes layout (see
/// <see cref="MapViewMemory"/>): keep the view it had under the layout being left, then either bring
/// back the view the new layout had or frame the fight's room.
/// </summary>
/// <param name="SaveAs">The layout to keep the current view under; <see cref="DockLayout.None"/> for none.</param>
/// <param name="RestoreFrom">The layout whose kept view comes back; <see cref="DockLayout.None"/> for none.</param>
/// <param name="FrameFight">True when the map frames the fight's room and its surroundings instead.</param>
public readonly record struct MapViewChange(DockLayout SaveAs, DockLayout RestoreFrom, bool FrameFight)
{
    /// <summary>Nothing changes: the layout did not change.</summary>
    public static MapViewChange None { get; } = new MapViewChange(DockLayout.None, DockLayout.None, false);
}
