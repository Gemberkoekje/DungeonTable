namespace DungeonTable.Core.Session;

/// <summary>
/// What this table has done in one keyed area: which of the creatures the book puts there have been
/// dealt with, and whatever the DM wrote down about the room.
/// </summary>
/// <remarks>
/// <para>
/// This is the answer to the question a campaign spanning months keeps asking — "have we already
/// been here, and did we kill the things in it?" Nothing else in the app can answer it: the dossier
/// says what the book puts in the room, the running fight says what is happening right now, and
/// neither remembers three sessions ago.
/// </para>
/// <para>
/// Creatures are recorded by node id, the same handle the encounter strip and the stat library
/// use, so a tick survives the prose being re-scanned. An id that stops appearing in an area's
/// encounter is simply never matched — harmless, and it comes back if the data edit is reverted.
/// There is no "the whole area is cleared" flag: that is read off the ticks, so the two can never
/// disagree.
/// </para>
/// <para>
/// One somebody or one band listed in several rooms is recorded in the room where it happened, and
/// what is left of them in any room is worked out from every room's record: Grask dealt with in the
/// workshop is dealt with on the stair too, unless he fled the workshop.
/// </para>
/// </remarks>
public sealed class AreaProgress
{
    /// <summary>The area's node id.</summary>
    public string AreaNodeId { get; set; } = string.Empty;

    /// <summary>The node ids of the creatures dealt with here, every one listed, in no particular order.</summary>
    public IReadOnlyList<string> ClearedCreatureIds { get; set; } = Array.Empty<string>();

    /// <summary>
    /// The creatures counted down one at a time here: how many were dealt with, and how many fled.
    /// Added after <see cref="ClearedCreatureIds"/>, with an empty default, so an older build reads the
    /// document and loses only these counts.
    /// </summary>
    public IReadOnlyList<CreatureCount> CreatureCounts { get; set; } = Array.Empty<CreatureCount>();

    /// <summary>The DM's own note about the room; an empty string when none is written.</summary>
    public string Note { get; set; } = string.Empty;
}
