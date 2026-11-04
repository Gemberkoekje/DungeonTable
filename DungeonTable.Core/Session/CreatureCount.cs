namespace DungeonTable.Core.Session;

/// <summary>
/// How many of one creature an area's listing has lost, counted one at a time: dealt with (killed,
/// taken, won over) or fled. Kept beside <see cref="AreaProgress.ClearedCreatureIds"/>, which says the
/// whole listing is dealt with at once.
/// </summary>
/// <remarks>
/// The two are different facts. Dealt with is for good: for one somebody or a band, listed in several
/// rooms, it counts in every room that lists them. Fled is about this room only: the boss who runs
/// from the watch post is gone from it, and still up in the hall where he makes his stand.
/// </remarks>
public sealed class CreatureCount
{
    /// <summary>The creature's node id, as the encounter strip and the stat library know it.</summary>
    public string CreatureId { get; set; } = string.Empty;

    /// <summary>How many of them have been dealt with here.</summary>
    public int Dealt { get; set; }

    /// <summary>How many of them fled from here, still up somewhere else.</summary>
    public int Fled { get; set; }
}
