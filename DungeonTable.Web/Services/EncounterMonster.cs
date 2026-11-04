namespace DungeonTable.Web.Services;

/// <summary>
/// One row of the Room tab's encounter strip: a creature this area lists. It carries the count to
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
    /// own, rather than as a numbered group named after its stat block. A band is not one.
    /// </summary>
    public bool Individual { get; init; }

    /// <summary>True when the area lists it as something the players must not know is there yet.</summary>
    public bool Secret { get; init; }

    /// <summary>
    /// True when it is one particular somebody or one band, which every area that lists it lists the
    /// same of: an entity, an NPC or a character, rather than a stat block listed as itself. What is
    /// dealt with of it in one room is dealt with in all of them (see <see cref="CreatureStanding"/>).
    /// </summary>
    public bool Shared { get; init; }

    /// <summary>
    /// How many there are in all when it is a band (an entity with a count above one), and zero
    /// otherwise. A band is added to a fight as a numbered group, as a kind of creature is.
    /// </summary>
    public int BandSize { get; init; }

    /// <summary>
    /// True when the area does not list it and it is here only because the DM moved it here (see
    /// <see cref="Core.Session.CreatureMove"/>): the row of the room that lists it, with none of its
    /// own listing.
    /// </summary>
    public bool MovedHere { get; init; }

    /// <summary>
    /// This row as it shows in a room the DM moved it to that does not list it: no listing of its own,
    /// no dice and no notes, since those were written about the other room.
    /// </summary>
    /// <param name="count">How many were moved here.</param>
    /// <returns>A copy of this row, moved here.</returns>
    public EncounterMonster Moved(int count) => new EncounterMonster
    {
        NodeId = NodeId,
        StatBlockNodeId = StatBlockNodeId,
        Name = Name,
        SuggestedCount = Math.Max(count, 1),
        CountExpression = string.Empty,
        Context = string.Empty,
        Citation = Citation,
        HasStatBlock = HasStatBlock,
        Individual = Individual,
        Secret = false,
        Shared = Shared,
        BandSize = BandSize,
        MovedHere = true,
    };

    /// <summary>The same row with another count, as the badges show what is left of it.</summary>
    /// <param name="count">The count to carry instead of the authored one.</param>
    /// <returns>A copy of this row with that count and no dice.</returns>
    public EncounterMonster WithCount(int count) => new EncounterMonster
    {
        NodeId = NodeId,
        StatBlockNodeId = StatBlockNodeId,
        Name = Name,
        SuggestedCount = count,
        CountExpression = string.Empty,
        Context = Context,
        Citation = Citation,
        HasStatBlock = HasStatBlock,
        Individual = Individual,
        Secret = Secret,
        Shared = Shared,
        BandSize = BandSize,
        MovedHere = MovedHere,
    };
}
