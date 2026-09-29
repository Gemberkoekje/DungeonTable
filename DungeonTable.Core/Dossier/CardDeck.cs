namespace DungeonTable.Core.Dossier;

/// <summary>
/// A card deck the DM draws from at the table: the cards, what one of them is called, and what a draw
/// does to the deck. A deck is content, one <c>deck-*.json</c> each, so a campaign authors as many as
/// it uses and the app knows none of them by name.
/// </summary>
public sealed class CardDeck
{
    /// <summary>
    /// Stable id, unique among the campaign's decks ("village-rumours"). Drawn cards are recorded
    /// under it, so renaming the file keeps them and changing the id forgets them.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The deck's title ("Village Rumours").</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Source page(s) the deck was taken from ("12", "12-13"), or an empty string.</summary>
    public string Pages { get; init; } = string.Empty;

    /// <summary>What one card is called ("rumour", "omen"); an empty string reads as "card".</summary>
    public string CardNoun { get; init; } = string.Empty;

    /// <summary>
    /// The plural of <see cref="CardNoun"/>, for a noun that does not take a plain "s"
    /// ("prophecies"); an empty string adds the "s".
    /// </summary>
    public string CardNounPlural { get; init; } = string.Empty;

    /// <summary>What a draw does to the deck: every card stays in, or a drawn card is kept out.</summary>
    public DeckDrawRule DrawRule { get; init; } = DeckDrawRule.None;

    /// <summary>
    /// The Draw button's tooltip, in the deck's own terms; an empty string gets one built from the
    /// rule and the card noun.
    /// </summary>
    public string DrawHint { get; init; } = string.Empty;

    /// <summary>When the deck is drawn from, in one sentence, so the Draw button explains itself.</summary>
    public string Usage { get; init; } = string.Empty;

    /// <summary>The cards, in printed order.</summary>
    public IReadOnlyList<DeckCard> Cards { get; init; } = Array.Empty<DeckCard>();
}
