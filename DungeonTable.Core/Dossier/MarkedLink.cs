namespace DungeonTable.Core.Dossier;

/// <summary>
/// One link an author wrote into dossier prose, as <c>[[target]]</c> or <c>[[target|shown text]]</c>:
/// where the markup sits in the raw text, what it points at, and the words to show in its place.
/// Found by <see cref="LinkMarkup.Parse"/>; what the target actually names is decided by whoever
/// resolves it against the loaded content.
/// </summary>
public sealed class MarkedLink
{
    /// <summary>Zero-based index of the opening <c>[[</c> in the raw text.</summary>
    public int Start { get; init; }

    /// <summary>Length of the whole markup, from <c>[[</c> through <c>]]</c>.</summary>
    public int Length { get; init; }

    /// <summary>The target as written, trimmed ("bandit", "area 6d", "L2 area 14", "wenna-brask").</summary>
    public string Target { get; init; } = string.Empty;

    /// <summary>The text to show in place of the markup, trimmed; empty when the author gave none.</summary>
    public string Shown { get; init; } = string.Empty;
}
