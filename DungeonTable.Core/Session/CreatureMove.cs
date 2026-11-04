namespace DungeonTable.Core.Session;

/// <summary>
/// Some of a creature the DM moved from one area to another: the guard who stayed behind when the
/// boss fled, or the survivors of a fight who fall back to the next room. The book lists who is where;
/// this is the table deciding otherwise.
/// </summary>
/// <remarks>
/// <para>
/// Moves are kept in the order they were made. For a kind of creature or a band they add up: a room has
/// what it lists, plus what was moved into it, less what was moved out. One somebody is in one place,
/// so for him only the last move counts: he is in the room it moved him to and nowhere else.
/// </para>
/// <para>
/// The room a creature is moved to need not list it. <see cref="ListedInAreaNodeId"/> names a room that
/// does, so the destination can show the creature as that room lists it (its name, its stat block)
/// without keeping a copy of either here.
/// </para>
/// </remarks>
public sealed class CreatureMove
{
    /// <summary>The creature's node id, as the encounter strip and the stat library know it.</summary>
    public string CreatureId { get; set; } = string.Empty;

    /// <summary>The area they were moved out of.</summary>
    public string FromAreaNodeId { get; set; } = string.Empty;

    /// <summary>The area they were moved to.</summary>
    public string ToAreaNodeId { get; set; } = string.Empty;

    /// <summary>How many of them were moved; always one for one somebody.</summary>
    public int Count { get; set; }

    /// <summary>
    /// An area whose own listing names the creature: the one they were first moved out of, carried along
    /// when they are moved on from a room that does not list them.
    /// </summary>
    public string ListedInAreaNodeId { get; set; } = string.Empty;
}
