namespace DungeonTable.Core.Session;

/// <summary>
/// The persisted half of the shared reveal state: which map the table is on, everything the DM has
/// revealed to the projector, which doors stand open, and where the player viewport is framed.
/// </summary>
/// <remarks>
/// The one-step clear-reveals undo snapshot is deliberately <b>not</b> here. It exists to take back a
/// misclick within seconds; carrying it across a restart would offer to undo a clear from another
/// evening, which is worse than not offering it at all.
/// </remarks>
public sealed class RevealSnapshot
{
    /// <summary>The id of the map the reveal state belongs to; an empty string when none is loaded.</summary>
    public string CurrentMapId { get; set; } = string.Empty;

    /// <summary>
    /// The map whose default-open doors and starting viewport have already been seeded. Stored so a
    /// restart (or a second DM circuit) re-opens the table where it was left rather than re-seeding
    /// over the DM's own framing and opened doors.
    /// </summary>
    public string SeededMapId { get; set; } = string.Empty;

    /// <summary>Region ids revealed to the player view.</summary>
    public IReadOnlyList<string> RevealedRegionIds { get; set; } = Array.Empty<string>();

    /// <summary>Feature marker and object ids revealed to the player view.</summary>
    public IReadOnlyList<string> RevealedFeatureIds { get; set; } = Array.Empty<string>();

    /// <summary>The door object ids standing open.</summary>
    public IReadOnlyList<string> OpenDoorIds { get; set; } = Array.Empty<string>();

    /// <summary>The grid cells the DM has painted with the fog brush.</summary>
    public IReadOnlyList<FogCell> FogCells { get; set; } = Array.Empty<FogCell>();

    /// <summary>The player projector's viewport rectangle, in map world units.</summary>
    public ViewportSnapshot PlayerViewport { get; set; } = new ViewportSnapshot();

    /// <summary>
    /// The player screen's last reported width / height ratio. Restored so the DM's framing is the
    /// right shape before the projector reconnects and reports it again.
    /// </summary>
    public double PlayerAspect { get; set; }

    /// <summary>
    /// The picture laid over the map on the projector, and the tray it was picked from.
    /// Additive and default-empty, so a build that predates it restores the rest of the table
    /// unharmed — which is why <see cref="TableSnapshot.CurrentVersion"/> was not bumped for it.
    /// </summary>
    public ShownArtSnapshot ShownArt { get; set; } = new ShownArtSnapshot();
}
