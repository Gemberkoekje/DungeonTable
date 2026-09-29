using System.Collections.Generic;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.Rules;

/// <summary>
/// The rules for a deck: it says what it is and how it is drawn, and every card can be drawn and read
/// out. A deck need not cite <c>pages</c>: that is provenance for a deck taken from a book.
/// </summary>
internal static class DeckAudit
{
    /// <summary>
    /// Everything the Rules tab needs from a deck that it does not say: an id (without one the store
    /// skips the whole deck, having nowhere to record what it dealt), a title, when to draw, and how.
    /// </summary>
    /// <param name="deck">A deck document, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> Undescribed(CardDeck deck)
    {
        var faults = new List<string>();
        string which = $"The deck {Check.Named(deck.Id, $"'{deck.Title}'")}";
        Check.Written(faults, deck.Id, $"{which} has no id, so the app skips it.");
        Check.Written(faults, deck.Title, $"{which} has no title.");
        Check.Written(faults, deck.Usage, $"{which} says nothing of when to draw from it.");
        if (deck.DrawRule == DeckDrawRule.None)
        {
            faults.Add($"{which} says nothing of how it is drawn: write fresh or kept.");
        }

        return faults;
    }

    /// <summary>Every other deck document with this deck's id. The store keeps the last of them.</summary>
    /// <param name="fileName">This deck's document.</param>
    /// <param name="deck">This deck, as written.</param>
    /// <param name="others">The other deck documents, by name.</param>
    /// <returns>One sentence per clash (empty when there are none).</returns>
    internal static IReadOnlyList<string> WrittenTwice(string fileName, CardDeck deck, IEnumerable<(string File, CardDeck Deck)> others) =>
        string.IsNullOrWhiteSpace(deck.Id)
            ? Array.Empty<string>()
            : others
                .Where(other => !string.Equals(other.File, fileName, StringComparison.Ordinal)
                    && string.Equals(other.Deck.Id, deck.Id, StringComparison.Ordinal))
                .Select(other => $"{other.File} is the deck '{deck.Id}' too: the app keeps only one of them.")
                .ToList();

    /// <summary>
    /// Every card that cannot be drawn or read out: no id or a repeated one (drawn cards are recorded by
    /// id), no name, or nothing to read.
    /// </summary>
    /// <param name="deck">A deck document, as written.</param>
    /// <returns>One sentence per fault (empty when there are none).</returns>
    internal static IReadOnlyList<string> BadCards(CardDeck deck)
    {
        var faults = new List<string>();
        for (int i = 0; i < deck.Cards.Count; i++)
        {
            DeckCard card = deck.Cards[i];
            string which = $"The deck '{deck.Id}', card {Check.Named(card.Id, $"cards[{i}]")},";
            Check.Written(faults, card.Id, $"{which} has no id, and a drawn card is recorded by it.");
            Check.Written(faults, card.Name, $"{which} has no name.");
            if (string.IsNullOrWhiteSpace(card.Body) && card.Effects.Count == 0)
            {
                faults.Add($"{which} has nothing to read out: no body and no effects.");
            }
        }

        faults.AddRange(Check.Repeated(deck.Cards.Select(card => card.Id))
            .Select(id => $"The deck '{deck.Id}' has two cards with the id '{id}': drawing one would mark both."));
        return faults;
    }
}
