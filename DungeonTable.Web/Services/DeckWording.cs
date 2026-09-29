using System;
using System.Globalization;
using DungeonTable.Core.Dossier;

namespace DungeonTable.Web.Services;

/// <summary>
/// The words the Rules tab uses for a deck: what its cards are called and what its buttons say. Built
/// from the deck's own <see cref="CardDeck.CardNoun"/> and <see cref="CardDeck.DrawRule"/>, so every
/// deck reads in its own terms ("Draw a random omen") without the app knowing any deck by name.
/// </summary>
public static class DeckWording
{
    /// <summary>What one card is called: the deck's noun, or "card" when it names none.</summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The singular noun, in the case it was authored in.</returns>
    public static string Noun(CardDeck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        string noun = (deck.CardNoun ?? string.Empty).Trim();
        return noun.Length > 0 ? noun : "card";
    }

    /// <summary>What several cards are called: the deck's own plural, else the noun with an "s".</summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The plural noun.</returns>
    public static string Plural(CardDeck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        string plural = (deck.CardNounPlural ?? string.Empty).Trim();
        return plural.Length > 0 ? plural : Noun(deck) + "s";
    }

    /// <summary>A number of cards, in the deck's terms ("1 omen", "4 omens").</summary>
    /// <param name="deck">The deck.</param>
    /// <param name="count">How many.</param>
    /// <returns>The count with its noun.</returns>
    public static string Count(CardDeck deck, int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? Noun(deck) : Plural(deck))}");

    /// <summary>
    /// The Draw button's tooltip: the deck's own hint, else one that says what the draw rule does.
    /// </summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The tooltip.</returns>
    public static string DrawHint(CardDeck deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        string hint = (deck.DrawHint ?? string.Empty).Trim();
        if (hint.Length > 0)
        {
            return hint;
        }

        return deck.DrawRule == DeckDrawRule.Kept
            ? $"Draw one of the {Plural(deck)} not handed out yet"
            : $"Draw a random {Noun(deck)}";
    }

    /// <summary>The tooltip on a kept deck's "n / m left" count ("Rumours still in the deck").</summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The tooltip.</returns>
    public static string LeftHint(CardDeck deck) => Capitalised(Plural(deck)) + " still in the deck";

    /// <summary>The tooltip on a kept deck's Reset button.</summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The tooltip.</returns>
    public static string ResetHint(CardDeck deck) => $"Put every drawn {Noun(deck)} back in the deck";

    /// <summary>What a kept deck says once every card is dealt out.</summary>
    /// <param name="deck">The deck.</param>
    /// <returns>The sentence.</returns>
    public static string AllDrawn(CardDeck deck) =>
        $"Every {Noun(deck)} has been drawn. Reset the deck to hand these out again.";

    private static string Capitalised(string text) =>
        text.Length == 0 ? text : string.Concat(char.ToUpperInvariant(text[0]).ToString(), text.AsSpan(1));
}
