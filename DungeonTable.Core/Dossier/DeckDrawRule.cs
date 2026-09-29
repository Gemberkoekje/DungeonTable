namespace DungeonTable.Core.Dossier;

/// <summary>
/// What drawing a card does to its deck. The deck document says which rule it follows, so the app
/// needs to know nothing about any particular deck.
/// </summary>
public enum DeckDrawRule
{
    /// <summary>Unset. Read as <see cref="Fresh"/>, the rule that can never hand a card out twice by mistake.</summary>
    None = 0,

    /// <summary>
    /// Every draw is from the whole deck: the card goes straight back in, and nothing is remembered
    /// (an omen read at every threshold).
    /// </summary>
    Fresh = 1,

    /// <summary>
    /// A drawn card leaves the deck until the deck is reset (a handout the players keep), and the
    /// drawn set persists with the rest of the table.
    /// </summary>
    Kept = 2,
}
