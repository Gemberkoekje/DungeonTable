namespace DungeonTable.Core.Dossier;

/// <summary>
/// One card of a deck: an omen with its boon and bane, say, or a rumour the players keep.
/// </summary>
public sealed class DeckCard
{
    /// <summary>Stable id, unique within its deck ("cracked-bell", "the-millers-debt").</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The card's name as printed ("Cracked Bell", "The Miller's Debt").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The line under the name ("Omen of Warning"); empty when none.</summary>
    public string Subtitle { get; init; } = string.Empty;

    /// <summary>The card's prose. Empty for a card whose text is all in <see cref="Effects"/>.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// The card's named effects ("Bane Effect", "Boon Effect"), or none. Reuses
    /// <see cref="DossierBlock"/> so the panel renders them exactly the way it renders every other
    /// headed block, cross-references and all.
    /// </summary>
    public IReadOnlyList<DossierBlock> Effects { get; init; } = Array.Empty<DossierBlock>();
}
