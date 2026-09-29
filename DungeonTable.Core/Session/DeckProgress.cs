namespace DungeonTable.Core.Session;

/// <summary>
/// The cards already drawn from one deck that keeps its drawn cards: dealt out, and not offered again
/// until the deck is reset.
/// </summary>
/// <remarks>
/// Cards are recorded by id, like quest beats, so re-ordering a deck document does not change which
/// cards read as drawn, and an id the deck no longer has is simply never matched.
/// </remarks>
public sealed class DeckProgress
{
    /// <summary>The deck this belongs to (<c>CardDeck.Id</c>).</summary>
    public string DeckId { get; set; } = string.Empty;

    /// <summary>The ids of the cards drawn, in the order they were drawn.</summary>
    public IReadOnlyList<string> DrawnCardIds { get; set; } = Array.Empty<string>();
}
