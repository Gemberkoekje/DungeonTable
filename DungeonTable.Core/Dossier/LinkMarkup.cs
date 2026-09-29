using System.Text;
using System.Text.RegularExpressions;

namespace DungeonTable.Core.Dossier;

/// <summary>
/// Reads the links authors write into dossier prose: <c>[[target]]</c>, or <c>[[target|shown text]]</c>
/// when the words on the page should differ from the target ("Four [[bandit|human bandits]] play
/// cards; a door leads to [[area 6d]].").
/// </summary>
/// <remarks>
/// <para>
/// Purely lexical. What a target names (an area, a stat block, a person) is decided by whoever
/// resolves it against the loaded content; this only finds the markup and takes it apart.
/// </para>
/// <para>
/// A target cannot contain <c>[</c>, <c>]</c>, <c>|</c> or a line break, and shown text cannot contain
/// a bracket or a line break, so a link never nests or runs across paragraphs. Anything else that
/// merely looks like markup (an unclosed <c>[[</c>, a bracket inside a link) is left as ordinary text.
/// An empty target (<c>[[]]</c>, <c>[[ |x]]</c>) is still a link, so the content checks can report it
/// as broken rather than letting it show as literal brackets.
/// </para>
/// </remarks>
public static partial class LinkMarkup
{
    /// <summary>The characters every link starts with; a text without them has no markup.</summary>
    public const string Opening = "[[";

    /// <summary>
    /// True when <paramref name="text"/> may contain a link. A cheap test for the common case, so
    /// callers can skip the parse for the prose nobody has marked up.
    /// </summary>
    /// <param name="text">The text to test; null or empty has no markup.</param>
    /// <returns><c>true</c> when the text contains <c>[[</c>.</returns>
    public static bool Contains(string text) =>
        !string.IsNullOrEmpty(text) && text.Contains(Opening, StringComparison.Ordinal);

    /// <summary>Finds every link in a block of prose, in the order they appear.</summary>
    /// <param name="text">The raw prose; null or empty yields no links.</param>
    /// <returns>The links in ascending <see cref="MarkedLink.Start"/> order (possibly empty).</returns>
    public static IReadOnlyList<MarkedLink> Parse(string text)
    {
        if (!Contains(text))
        {
            return Array.Empty<MarkedLink>();
        }

        MatchCollection matches = LinkPattern().Matches(text);
        if (matches.Count == 0)
        {
            return Array.Empty<MarkedLink>();
        }

        var links = new MarkedLink[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            Match match = matches[i];
            links[i] = new MarkedLink
            {
                Start = match.Index,
                Length = match.Length,
                Target = match.Groups["target"].Value.Trim(),
                Shown = match.Groups["shown"].Value.Trim(),
            };
        }

        return links;
    }

    /// <summary>
    /// The prose with every link replaced by its words: the shown text where the author gave one,
    /// otherwise the target as written. For the places that show dossier text without resolving
    /// anything; a resolver can do better for <c>[[target]]</c> (a person's name rather than their id).
    /// </summary>
    /// <param name="text">The raw prose; null yields an empty string.</param>
    /// <returns>The prose without markup.</returns>
    public static string Plain(string text)
    {
        IReadOnlyList<MarkedLink> links = Parse(text);
        if (links.Count == 0)
        {
            return text ?? string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        int position = 0;
        foreach (MarkedLink link in links)
        {
            builder.Append(text, position, link.Start - position);
            builder.Append(Words(text, link));
            position = link.Start + link.Length;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    /// <summary>
    /// What a link shows when nothing better is known: its shown text, else its target, else (for
    /// an empty <c>[[]]</c>) the markup itself, so a broken link never silently vanishes from the page.
    /// </summary>
    /// <param name="text">The raw prose the link was found in.</param>
    /// <param name="link">The link.</param>
    /// <returns>The words to show.</returns>
    public static string Words(string text, MarkedLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (link.Shown.Length > 0)
        {
            return link.Shown;
        }

        return link.Target.Length > 0 ? link.Target : (text ?? string.Empty).Substring(link.Start, link.Length);
    }

    [GeneratedRegex(@"\[\[(?<target>[^\[\]|\r\n]*)(?:\|(?<shown>[^\[\]\r\n]*))?\]\]", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex LinkPattern();
}
