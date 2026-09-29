using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// The Room Editor's link box links by searching, and a search only finds what is already written.
/// A map is usually annotated before its level's areas are, so the box also takes an id typed in
/// full: the region then opens its area the day a level file writes an area with that id, the way a
/// quest beat's destination lights up.
/// </summary>
public static class TypedNodeId
{
    /// <summary>
    /// The id the link box offers to link exactly as typed: the query, trimmed, when it could be an
    /// id (it has no spaces) and the search did not already find something with exactly that id.
    /// </summary>
    /// <param name="query">What was typed into the link box.</param>
    /// <param name="matches">What the search found for it.</param>
    /// <returns>The id to offer, or an empty string when there is nothing to offer.</returns>
    public static string Offer(string query, IReadOnlyList<SearchMatch> matches)
    {
        string id = (query ?? string.Empty).Trim();
        if (id.Length == 0 || id.Any(char.IsWhiteSpace))
        {
            return string.Empty;
        }

        bool found = (matches ?? Array.Empty<SearchMatch>())
            .Any(match => match is not null && string.Equals(match.Id, id, StringComparison.Ordinal));
        return found ? string.Empty : id;
    }
}
