using System.Linq;

namespace DungeonTable.Core.Dossier;

/// <summary>
/// The fixed vocabulary a <see cref="Relation"/> is written in:
/// sixteen kinds the app can style and filter by, and <c>related</c>, which only the machine-made book
/// index may use. The set is extended deliberately, never by an extractor.
/// </summary>
/// <remarks>
/// A kind is kept as the string the author wrote rather than parsed into an enum, so one mistyped
/// kind shows as written and is reported by the content checks, instead of costing the whole level
/// file at load. The kebab-case the documents use ("plots-against") is also not what the store's enum
/// converter reads.
/// </remarks>
public static class RelationKinds
{
    /// <summary>The one kind only the book index may use, for a book link that fits no other.</summary>
    public const string Related = "related";

    // kind -> (from a's side, from b's side). A mutual kind reads the same both ways.
    private static readonly Dictionary<string, (string FromA, string FromB)> Phrases =
        new Dictionary<string, (string FromA, string FromB)>(StringComparer.Ordinal)
        {
            ["ally"] = ("ally of", "ally of"),
            ["rival"] = ("rival of", "rival of"),
            ["enemy"] = ("enemy of", "enemy of"),
            ["family"] = ("family of", "family of"),
            ["leads"] = ("leads", "led by"),
            ["member-of"] = ("member of", "has as a member"),
            ["serves"] = ("serves", "served by"),
            ["controls"] = ("controls", "controlled by"),
            ["fears"] = ("fears", "feared by"),
            ["hunts"] = ("hunts", "hunted by"),
            ["plots-against"] = ("plots against", "plotted against by"),
            ["owes"] = ("owes", "owed by"),
            ["protects"] = ("protects", "protected by"),
            ["loves"] = ("loves", "loved by"),
            ["runs"] = ("runs", "run by"),
            ["located-in"] = ("located in", "home to"),
            [Related] = ("related to", "related to"),
        };

    private static readonly HashSet<string> Mutual =
        new HashSet<string>(new[] { "ally", "rival", "enemy", "family", Related }, StringComparer.Ordinal);

    /// <summary>The sixteen kinds an author may write, in a fixed order.</summary>
    public static IReadOnlyList<string> Authored { get; } = Phrases.Keys.Where(kind => kind != Related).ToArray();

    /// <summary>True for one of the sixteen authored kinds (not <see cref="Related"/>).</summary>
    /// <param name="kind">The kind as written.</param>
    /// <returns><c>true</c> when an author may use it.</returns>
    public static bool IsAuthored(string kind) =>
        kind is not null && !string.Equals(kind, Related, StringComparison.Ordinal) && Phrases.ContainsKey(kind);

    /// <summary>True for a kind that reads the same from either end ("rival of").</summary>
    /// <param name="kind">The kind as written.</param>
    /// <returns><c>true</c> when mutual.</returns>
    public static bool IsMutual(string kind) => kind is not null && Mutual.Contains(kind);

    /// <summary>How a relation reads from one of its ends: "plots against" from a, "plotted against by" from b.</summary>
    /// <param name="kind">The kind as written.</param>
    /// <param name="fromA">True to read it from the relation's <c>a</c> end.</param>
    /// <returns>The phrase; an unknown kind reads as written, with its hyphens as spaces.</returns>
    public static string Phrase(string kind, bool fromA)
    {
        string written = kind ?? string.Empty;
        if (Phrases.TryGetValue(written, out (string FromA, string FromB) phrase))
        {
            return fromA ? phrase.FromA : phrase.FromB;
        }

        return written.Replace('-', ' ');
    }
}
