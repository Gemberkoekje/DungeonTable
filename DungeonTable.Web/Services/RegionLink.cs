using System.Collections.Generic;
using System.Linq;
using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// What a map region may link to in the Room Editor: an area, written already or typed in full ahead
/// of its floor. Clicking a region on the DM screen opens its area in the panel, so a region linked
/// to a person, a stat block or a book entry opened nothing; the link box offered them all, since a
/// marker or an object may link to anything.
/// </summary>
public static class RegionLink
{
    // How far down a search by id to look for the person the id names. An id matches itself as a
    // name, so it is near the top; the rest is headroom.
    private const int PersonSearchLimit = 50;

    /// <summary>The areas among a search's results, best first, at most <paramref name="limit"/>.</summary>
    /// <param name="matches">What the search found.</param>
    /// <param name="limit">How many to offer.</param>
    /// <returns>The areas.</returns>
    public static IReadOnlyList<SearchMatch> Areas(IEnumerable<SearchMatch> matches, int limit) =>
        (matches ?? Array.Empty<SearchMatch>())
            .Where(match => match is not null && string.Equals(match.Category, SearchMatch.AreaCategory, StringComparison.Ordinal))
            .Take(limit)
            .ToList();

    /// <summary>
    /// The id typed into a region's link box, offered to link as typed: as <see cref="TypedNodeId"/>
    /// offers it, unless something that is not an area is written under that id.
    /// </summary>
    /// <param name="query">What was typed.</param>
    /// <param name="matches">The areas the search found for it.</param>
    /// <param name="writtenAs">What is written under an id: <see cref="CrossRefKind.None"/> when nothing is.</param>
    /// <returns>The id to offer, or an empty string.</returns>
    public static string Offer(string query, IReadOnlyList<SearchMatch> matches, Func<string, CrossRefKind> writtenAs)
    {
        ArgumentNullException.ThrowIfNull(writtenAs);

        string id = TypedNodeId.Offer(query, matches);
        return id.Length > 0 && IsSomethingElse(writtenAs(id)) ? string.Empty : id;
    }

    /// <summary>
    /// Why the id typed into a region's link box cannot be linked: it names something written that is
    /// not an area. Empty when it can be, or when nothing was typed that could be an id.
    /// </summary>
    /// <param name="query">What was typed.</param>
    /// <param name="matches">The areas the search found for it.</param>
    /// <param name="writtenAs">What is written under an id: <see cref="CrossRefKind.None"/> when nothing is.</param>
    /// <returns>The reason, or an empty string.</returns>
    public static string Refusal(string query, IReadOnlyList<SearchMatch> matches, Func<string, CrossRefKind> writtenAs)
    {
        ArgumentNullException.ThrowIfNull(writtenAs);

        string id = TypedNodeId.Offer(query, matches);
        if (id.Length == 0)
        {
            return string.Empty;
        }

        CrossRefKind kind = writtenAs(id);
        return IsSomethingElse(kind)
            ? $"'{id}' is {What(kind)}, not an area. A region opens an area when it is clicked; link a marker to it instead."
            : string.Empty;
    }

    /// <summary>
    /// What is written under an id: the kind of its card, or, for a person (whose card the DM screen
    /// builds from their own dossier), what the search calls them. <see cref="CrossRefKind.None"/> when
    /// nothing is written under it yet.
    /// </summary>
    /// <param name="content">The content projection.</param>
    /// <param name="id">The id.</param>
    /// <returns>The kind.</returns>
    public static CrossRefKind WrittenAs(IContentProjection content, string id)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(id))
        {
            return CrossRefKind.None;
        }

        var card = content.GetReferenceCard(id);
        if (card.IsValid)
        {
            return card.Value.Kind;
        }

        SearchMatch named = content.SearchNodes(id, PersonSearchLimit)
            .FirstOrDefault(match => match is not null && string.Equals(match.Id, id, StringComparison.Ordinal));
        return named is null ? CrossRefKind.None : KindOf(named.Category);
    }

    /// <summary>True when an id a region links to is written, and is not an area.</summary>
    /// <param name="writtenAs">What is written under the id.</param>
    /// <returns><c>true</c> for a person, a stat block, an entity or a book entry.</returns>
    public static bool IsSomethingElse(CrossRefKind writtenAs) =>
        writtenAs is not CrossRefKind.None and not CrossRefKind.Area;

    /// <summary>What a kind of card is, in a sentence: "an NPC", "a stat block".</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The phrase.</returns>
    public static string What(CrossRefKind kind) => kind switch
    {
        CrossRefKind.Npc => "an NPC",
        CrossRefKind.Character => "a player character",
        CrossRefKind.Monster => "a creature",
        CrossRefKind.Spell => "a spell",
        CrossRefKind.Item => "an item",
        CrossRefKind.Faction => "a faction",
        CrossRefKind.Book => "a book entry",
        CrossRefKind.Area => "an area",
        _ => "something else",
    };

    // A search result's category as a kind: the campaign's own ("npc", "monster") and an entity's
    // ("creature", "faction"). Anything else written is still not an area.
    private static CrossRefKind KindOf(string category) => category switch
    {
        SearchMatch.AreaCategory => CrossRefKind.Area,
        "npc" => CrossRefKind.Npc,
        "character" => CrossRefKind.Character,
        "monster" or "creature" => CrossRefKind.Monster,
        "spell" => CrossRefKind.Spell,
        "faction" => CrossRefKind.Faction,
        "book" => CrossRefKind.Book,
        _ => CrossRefKind.Item,
    };
}
