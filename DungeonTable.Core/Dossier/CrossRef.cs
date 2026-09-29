namespace DungeonTable.Core.Dossier;

/// <summary>
/// A clickable cross-reference span inside a <see cref="DossierBlock"/> body: the words it shows,
/// what it points at, and where it sits in the body so the panel can render it as a link in place.
/// Produced by the cross-reference engine, either for an authored <c>[[…]]</c> link or for a name it
/// found in unmarked prose.
/// </summary>
public sealed class CrossRef
{
    /// <summary>
    /// The words the reference shows. For a name found in prose they are the matched text itself
    /// ("bugbears"); for an authored link they replace the markup, so they differ from the raw span
    /// between <see cref="Start"/> and <see cref="Length"/> ("human bandits" for
    /// <c>[[bandit|human bandits]]</c>).
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>What kind of thing the reference points at.</summary>
    public CrossRefKind Kind { get; init; } = CrossRefKind.None;

    /// <summary>
    /// The id of the reference target (a node id for an area, stat block or spell; the roster id for
    /// an NPC or a player character), or an empty string for a non-navigating reference such as an
    /// ability-check pill or a <see cref="CrossRefKind.Unresolved"/> link.
    /// </summary>
    public string TargetId { get; init; } = string.Empty;

    /// <summary>Zero-based index of the span within the block body; for an authored link, of its <c>[[</c>.</summary>
    public int Start { get; init; }

    /// <summary>Length of the span in characters; for an authored link, the whole markup through <c>]]</c>.</summary>
    public int Length { get; init; }
}
