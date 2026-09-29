using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Serves the extracted monster and spell stat blocks that back the combatant detail pane and the
/// upgraded reference drawer. Implemented by the file-system adapter that reads the
/// <c>data/statblocks/*.json</c> documents at startup, and again when one changes on disk. A
/// campaign carries only the blocks it writes up, so a monster it never wrote up simply has no
/// block — callers must treat that as routine, never as an error.
/// </summary>
public interface IStatLibrary
{
    /// <summary>Gets the stat block for a monster or NPC by its node id ("data_statblocks_monsters_bugbear").</summary>
    /// <param name="nodeId">The node id of the creature.</param>
    /// <returns>The stat block, or an invalid result when none is extracted for the node.</returns>
    Result<StatBlock> GetMonster(string nodeId);

    /// <summary>Gets the full spell entry for a spell by its node id ("data_statblocks_spells_dispel_magic").</summary>
    /// <param name="nodeId">The node id of the spell.</param>
    /// <returns>The spell entry, or an invalid result when none is extracted for the node.</returns>
    Result<SpellEntry> GetSpell(string nodeId);

    /// <summary>
    /// Every extracted monster stat block. For callers that index the whole set rather than look one
    /// up: the link resolver, which finds a block by the slug of its name (<c>[[flesh-golem]]</c>).
    /// </summary>
    /// <returns>The stat blocks, in load order (possibly empty).</returns>
    IReadOnlyList<StatBlock> AllMonsters();

    /// <summary>Every extracted spell entry; the counterpart to <see cref="AllMonsters"/>.</summary>
    /// <returns>The spell entries, in load order (possibly empty).</returns>
    IReadOnlyList<SpellEntry> AllSpells();

    /// <summary>Indicates whether a stat block is extracted for the given monster node id.</summary>
    /// <param name="nodeId">The node id to test.</param>
    /// <returns><c>true</c> when <see cref="GetMonster"/> would return a valid result.</returns>
    bool HasMonster(string nodeId);

    /// <summary>
    /// Searches extracted monster stat blocks by name, ranked best-first, for the Battle tab's
    /// "add monster" search.
    /// </summary>
    /// <param name="query">A name substring; a blank query returns no matches.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <returns>The ranked matches, best first (possibly empty).</returns>
    IReadOnlyList<SearchMatch> SearchMonsters(string query, int limit);
}
