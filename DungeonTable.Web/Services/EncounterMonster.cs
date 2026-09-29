namespace DungeonTable.Web.Services;

/// <summary>
/// One row of the Info tab's encounter strip: a creature this area lists. It carries the count to
/// pre-fill and enough context for the DM to judge whether it belongs in the fight they are about to
/// start.
/// </summary>
public sealed class EncounterMonster
{
    /// <summary>
    /// What the row is and what its name opens: a stat block's node id
    /// ("data_statblocks_monsters_bugbear"), or an individual's id ("bell-warden"). The row's
    /// "dealt with" tick is recorded against it.
    /// </summary>
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// The stat block a combatant added from this row is filled in from. The same as
    /// <see cref="NodeId"/> for a stat-block row; the block an individual uses; empty for none.
    /// </summary>
    public string StatBlockNodeId { get; init; } = string.Empty;

    /// <summary>The creature's display name ("Bugbear", "the Bell-Warden").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>How many to pre-fill the stepper with, as authored.</summary>
    public int SuggestedCount { get; init; } = 1;

    /// <summary>The dice expression authored instead of a number ("1d4 + 1"), or an empty string.</summary>
    public string CountExpression { get; init; } = string.Empty;

    /// <summary>The author's notes on the creature here, side by side where it is listed more than once.</summary>
    public string Context { get; init; } = string.Empty;

    /// <summary>The source citation ("SRD 5.1 p.266"), or an empty string.</summary>
    public string Citation { get; init; } = string.Empty;

    /// <summary>
    /// True when the stat library has a block for it. False rows still add to a battle — with a
    /// name and editable HP/AC — because a missing extraction must never block a fight.
    /// </summary>
    public bool HasStatBlock { get; init; }

    /// <summary>
    /// True for one named individual, such as Grask: added under its own name, on a row of its
    /// own, rather than as a numbered group named after its stat block.
    /// </summary>
    public bool Individual { get; init; }

    /// <summary>True when the area lists it as something the players must not know is there yet.</summary>
    public bool Secret { get; init; }
}
