using System;
using System.Collections.Generic;

namespace DungeonTable.Web.Services;

/// <summary>
/// Marks the words of a query in a Find result's text, so the eye lands on them: every place any word
/// occurs, whatever its case, with overlapping and touching places joined into one mark.
/// </summary>
public static class TextHighlight
{
    /// <summary>Splits a text into stretches that match a word of the query and stretches that do not.</summary>
    /// <param name="text">The text shown.</param>
    /// <param name="query">The query, split into words on white space.</param>
    /// <returns>The stretches in order; one unmarked stretch when nothing matches, none for an empty text.</returns>
    public static IReadOnlyList<HighlightPart> Split(string text, string query)
    {
        string shown = text ?? string.Empty;
        if (shown.Length == 0)
        {
            return Array.Empty<HighlightPart>();
        }

        var marked = new bool[shown.Length];
        foreach (string word in (query ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            for (int at = shown.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = shown.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
            {
                Array.Fill(marked, true, at, word.Length);
            }
        }

        var parts = new List<HighlightPart>();
        int start = 0;
        for (int i = 1; i <= shown.Length; i++)
        {
            if (i == shown.Length || marked[i] != marked[start])
            {
                parts.Add(new HighlightPart(shown[start..i], marked[start]));
                start = i;
            }
        }

        return parts;
    }
}
