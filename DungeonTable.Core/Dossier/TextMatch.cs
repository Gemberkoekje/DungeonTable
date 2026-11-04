namespace DungeonTable.Core.Dossier;

/// <summary>
/// One hit of Find anything on the campaign's text: the passage the words were found in, and the stretch
/// of it around the first match, so that most lookups end with the answer in the results.
/// </summary>
public sealed class TextMatch
{
    /// <summary>The passage that holds every word looked for.</summary>
    public Passage Passage { get; init; } = new Passage();

    /// <summary>
    /// The passage's words around the first match, as plain text (link markup shown as its words), with an
    /// ellipsis where it was cut; the whole passage when it is short.
    /// </summary>
    public string Snippet { get; init; } = string.Empty;
}
