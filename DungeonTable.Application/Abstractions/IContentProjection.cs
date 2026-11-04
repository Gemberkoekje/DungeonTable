using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using Qowaiv.Validation.Abstractions;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Projects the content into DM-facing room briefings, reference cards and search results: the
/// authored content (areas with their creatures and exits, the entities a level declares, the people,
/// the stat blocks) and, below it, the book index.
/// </summary>
public interface IContentProjection
{
    /// <summary>
    /// Builds the <see cref="RoomBriefing"/> for an authored area.
    /// </summary>
    /// <param name="nodeId">The area's node id.</param>
    /// <returns>
    /// A valid result carrying the briefing, or an invalid result when no area with that id is
    /// authored, or the id is blank.
    /// </returns>
    Result<RoomBriefing> GetBriefing(string nodeId);

    /// <summary>
    /// Searches everything a DM might look for by name, ranked best-first: the entities a level
    /// declares, then the campaign's own areas, people, stat blocks and spells, then the book index.
    /// The Room Editor uses it to link a region, feature marker or object to its area.
    /// </summary>
    /// <param name="query">A name substring; a blank query returns no matches.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <returns>The ranked matches, best first (possibly empty).</returns>
    IReadOnlyList<SearchMatch> SearchNodes(string query, int limit);

    /// <summary>
    /// Searches the campaign's text, for Find anything: every passage of prose (the rules, the floors'
    /// facts, the rooms, the people, the quests, the session prep, the decks, the background, the party,
    /// the story) that holds every word of the query, ranked best-first, each with the stretch of it
    /// around the match.
    /// </summary>
    /// <param name="query">The words looked for; a blank query returns no matches.</param>
    /// <param name="limit">The maximum number of matches to return.</param>
    /// <returns>The ranked matches, best first (possibly empty).</returns>
    IReadOnlyList<TextMatch> FindText(string query, int limit);

    /// <summary>
    /// The passage with a key, as the content holds it now: what a Find result or a pin opens.
    /// </summary>
    /// <param name="key">The passage's key (see <see cref="PassageKeys"/>).</param>
    /// <returns>The passage, or an invalid result when no passage has that key.</returns>
    Result<Passage> GetPassage(string key);

    /// <summary>
    /// Builds a link-out <see cref="ReferenceCard"/> for what a cross-reference points at: an entity
    /// a level declares, from its description, stat block and the areas it appears in; a stat block or
    /// spell, from its name and source; or a book index entry, from what the book says. The DM panel
    /// peeks at it without leaving the room.
    /// </summary>
    /// <param name="nodeId">The id of the entity, stat block, spell or book entry.</param>
    /// <returns>A valid result carrying the card, or an invalid result when nothing has that id, or it is blank.</returns>
    Result<ReferenceCard> GetReferenceCard(string nodeId);

    /// <summary>
    /// The authored relations an entity, a person or an area takes part in, read from its side
    /// ("plots against Grask"). For the cards that are not built here: an NPC's or a player
    /// character's, which the DM screen shows from their own dossiers.
    /// </summary>
    /// <param name="id">The id of one end.</param>
    /// <returns>The relation lines (possibly empty).</returns>
    IReadOnlyList<RelationLine> RelationsOf(string id);

    /// <summary>
    /// The node id of the stat block an individual fights with: an entity a level declares, or one of
    /// the campaign's NPCs, by the <c>statBlock</c> its entry names. For the NPC cards that are not
    /// built here, as <see cref="RelationsOf"/> is.
    /// </summary>
    /// <param name="id">An entity's or an NPC's id.</param>
    /// <returns>
    /// The stat block's node id; an empty string when the id names no individual, or its entry names no
    /// stat block the library has extracted.
    /// </returns>
    string StatBlockOf(string id);

    /// <summary>
    /// What the book index says about an id: its entry's own card, with the book and page. Also for an
    /// id the campaign has an entry of its own for (the book's Wenna Brask, beside the NPC
    /// <c>wenna-brask</c>), whose card the book's entry then extends rather than replaces.
    /// </summary>
    /// <param name="id">An id the book index may have an entry for.</param>
    /// <returns>The entry's card, or an invalid result when the book index has no such entry.</returns>
    Result<ReferenceCard> GetBookCard(string id);
}
