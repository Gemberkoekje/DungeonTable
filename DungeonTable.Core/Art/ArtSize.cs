namespace DungeonTable.Core.Art;

/// <summary>
/// How large the shown picture is drawn on the projector, as a share of the player screen. Kept as
/// four named steps rather than a free percentage: the DM picks this mid-scene with one click, and
/// the map has to stay readable around all but the largest.
/// </summary>
public enum ArtSize
{
    /// <summary>Not stated; treated as <see cref="Medium"/>.</summary>
    None = 0,

    /// <summary>A corner portrait, most of the map still in view.</summary>
    Small = 1,

    /// <summary>The default: clearly the subject of the screen, map visible around it.</summary>
    Medium = 2,

    /// <summary>Large enough to dominate, with the map framing it.</summary>
    Large = 3,

    /// <summary>Fills the screen; the map is behind it.</summary>
    Full = 4,
}
