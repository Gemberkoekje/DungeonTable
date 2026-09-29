namespace DungeonTable.Core.Dossier;

/// <summary>
/// Every chapter of the table's prior-campaign history, in reading order. The wrapper exists so the
/// committed document has a root object the other dossier documents' shape matches, and so a second
/// outgoing-DM handoff drops in beside the first rather than replacing it.
/// </summary>
public sealed class StoryLibrary
{
    /// <summary>The chapters, in the order they should be read.</summary>
    public IReadOnlyList<StoryDossier> Chapters { get; init; } = Array.Empty<StoryDossier>();
}
