using DungeonTable.Core.Dossier;

namespace DungeonTable.Application.Abstractions;

/// <summary>
/// Finds the clickable cross-references in dossier prose: the links authors write into it
/// (<c>[[bandit|human bandits]]</c>, see <see cref="LinkMarkup"/>), the digital form of the book's
/// "bold name, go look it up" convention. Given a block of text, returns the spans that point at
/// something the DM can open (a stat block or spell, an NPC, faction, item or player character, a book
/// entry, or an area), so the DM panel can render them as links in place. Deterministic and
/// side-effect-free, so results may be cached per text.
/// </summary>
/// <remarks>
/// <para>
/// Every link yields a span covering the whole markup and carrying the words to show in its place,
/// whether or not its target resolves: a broken one is <see cref="CrossRefKind.Unresolved"/>, so
/// markup never reaches the page as brackets. Prose nobody has marked up stays plain text: a mention
/// is not a link until an author says so.
/// </para>
/// <para>
/// A scan is <b>level-scoped</b>, because an area key only means something relative to a floor:
/// "area 19" is a different room on every level of the dungeon. The caller says which floor's prose it
/// is reading, and a bare <c>[[area 19]]</c> is read on that floor; prose that belongs to no floor (the
/// quest log, the appendix decks) has to name the floor itself (<c>[[L2 area 14]]</c>).
/// </para>
/// </remarks>
public interface ICrossRefResolver
{
    /// <summary>
    /// Resolves the links in a block of prose that belongs to no particular floor. A link to an area
    /// resolves only when it names its floor (<c>[[L1 area 6d]]</c>).
    /// </summary>
    /// <param name="text">The prose to scan; a blank string yields no references.</param>
    /// <returns>The link spans in ascending start order (possibly empty).</returns>
    IReadOnlyList<CrossRef> Resolve(string text);

    /// <summary>
    /// Resolves the links in a block of prose read on a known floor, where a bare <c>[[area 19b]]</c>
    /// names that floor's area.
    /// </summary>
    /// <param name="text">The prose to scan; a blank string yields no references.</param>
    /// <param name="levelNodeId">The node id of the floor whose prose this is
    /// ("data_dossiers_level_1"); a blank id behaves exactly like <see cref="Resolve(string)"/>.</param>
    /// <returns>The link spans in ascending start order (possibly empty).</returns>
    IReadOnlyList<CrossRef> Resolve(string text, string levelNodeId);
}
