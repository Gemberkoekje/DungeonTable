namespace DungeonTable.Web.Services;

/// <summary>
/// One block of the area's own prose that has something to say about a creature in the fight — the
/// read-aloud sentence that put it there, an at-a-glance bullet, or a detail subsection like
/// "Bugbear Tactics". Shown inside the combatant detail pane so the DM never has to leave the Battle
/// tab to remember why this monster is in the room and how the book says it behaves.
/// </summary>
public sealed class EncounterNote
{
    /// <summary>Which part of the dossier this came from ("read-aloud", "at a glance", "detail").</summary>
    public string Section { get; init; } = string.Empty;

    /// <summary>The block's heading, or an empty string (the read-aloud has none).</summary>
    public string Heading { get; init; } = string.Empty;

    /// <summary>The block's prose.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// True when the block is DM-only and must never be read aloud. Carried straight through from
    /// the dossier so the pane can flag it exactly as the briefing panel does.
    /// </summary>
    public bool Secret { get; init; }
}
