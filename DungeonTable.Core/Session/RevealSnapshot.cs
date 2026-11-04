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
    /// The id of the map on the player projector. The DM can move to another map without the players
    /// following until the DM shows it to them, so this can differ from <see cref="CurrentMapId"/>; that
    /// map's reveals and framing are then in <see cref="OtherMaps"/>. Additive like
    /// <see cref="ShownArt"/>: blank in a document written before it existed, which is read as
    /// <see cref="CurrentMapId"/>, the map the projector always showed then.
    /// </summary>
    public string ProjectorMapId { get; set; } = string.Empty;

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

    /// <summary>
    /// What was left on each other map the table has been on, so going back to one shows it as the
    /// party left it. Never holds <see cref="CurrentMapId"/>: the fields above are that map's.
    /// Additive and default-empty like <see cref="ShownArt"/>: a build that predates it restores the
    /// map in play and loses only the other maps' reveals, so
    /// <see cref="TableSnapshot.CurrentVersion"/> was not bumped for it either.
    /// </summary>
    public IReadOnlyList<MapRevealSnapshot> OtherMaps { get; set; } = Array.Empty<MapRevealSnapshot>();

    /// <summary>
    /// The party token on the map in play: where it stands and how far the party sees. Additive like
    /// <see cref="ShownArt"/> (see <see cref="PartyTokenSnapshot"/>).
    /// </summary>
    public PartyTokenSnapshot PartyToken { get; set; } = new PartyTokenSnapshot();

    /// <summary>
    /// Whether the projector follows the party token, re-framing when it leaves the middle of the
    /// projector's view. A setting of the table rather than of a map. Additive and off by default.
    /// </summary>
    public bool ProjectorFollowsParty { get; set; }

    /// <summary>
    /// How far the projector's picture of the map is turned, in quarter turns clockwise from 0 to 3: for
    /// a table the players sit around. A setting of the table, like
    /// <see cref="ProjectorFollowsParty"/>; each map's framing is kept in the same turn. Additive, and
    /// upright by default.
    /// </summary>
    public int PlayerTurns { get; set; }

    /// <summary>
    /// The size of the projector's view the DM remembered, in grid squares, to frame another map at the
    /// same scale, so a square on the table stays the size of a miniature's base. Additive, and none by
    /// default.
    /// </summary>
    public ViewSizeSnapshot RememberedView { get; set; } = new ViewSizeSnapshot();
}
