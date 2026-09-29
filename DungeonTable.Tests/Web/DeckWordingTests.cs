using DungeonTable.Core.Dossier;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The Rules tab's words for a deck come from the deck: what a card is called, and what the Draw
/// button promises, which follows the deck's own draw rule unless the deck says it better.
/// </summary>
public sealed class DeckWordingTests
{
    [Fact]
    public void A_deck_that_names_its_cards_is_read_in_its_own_terms()
    {
        var omens = new CardDeck { CardNoun = " omen " };

        Assert.Equal("omen", DeckWording.Noun(omens));
        Assert.Equal("omens", DeckWording.Plural(omens));
        Assert.Equal("1 omen", DeckWording.Count(omens, 1));
        Assert.Equal("4 omens", DeckWording.Count(omens, 4));
        Assert.Equal("0 omens", DeckWording.Count(omens, 0));
    }

    [Fact]
    public void A_deck_that_names_nothing_has_cards()
    {
        var deck = new CardDeck { CardNoun = "  ", CardNounPlural = string.Empty };

        Assert.Equal("card", DeckWording.Noun(deck));
        Assert.Equal("cards", DeckWording.Plural(deck));
        Assert.Equal("Cards still in the deck", DeckWording.LeftHint(deck));
    }

    [Fact]
    public void A_noun_with_an_irregular_plural_says_so()
    {
        var deck = new CardDeck { CardNoun = "prophecy", CardNounPlural = "prophecies" };

        Assert.Equal("2 prophecies", DeckWording.Count(deck, 2));
        Assert.Equal("Prophecies still in the deck", DeckWording.LeftHint(deck));
    }

    [Fact]
    public void The_draw_hint_says_what_the_deck_does_with_a_drawn_card()
    {
        Assert.Equal(
            "Draw one of the rumours not handed out yet",
            DeckWording.DrawHint(new CardDeck { CardNoun = "rumour", DrawRule = DeckDrawRule.Kept }));
        Assert.Equal(
            "Draw a random omen",
            DeckWording.DrawHint(new CardDeck { CardNoun = "omen", DrawRule = DeckDrawRule.Fresh }));

        // Unset is drawn fresh, so it is described as fresh.
        Assert.Equal("Draw a random card", DeckWording.DrawHint(new CardDeck()));
    }

    [Fact]
    public void A_deck_that_writes_its_own_hint_is_taken_at_its_word()
    {
        var deck = new CardDeck
        {
            CardNoun = "omen",
            DrawRule = DeckDrawRule.Kept,
            DrawHint = "  Read one aloud when the bell is struck ",
        };

        Assert.Equal("Read one aloud when the bell is struck", DeckWording.DrawHint(deck));
    }

    [Fact]
    public void Reset_and_empty_deck_messages_use_the_noun()
    {
        var rumours = new CardDeck { CardNoun = "rumour" };

        Assert.Equal("Put every drawn rumour back in the deck", DeckWording.ResetHint(rumours));
        Assert.Equal("Every rumour has been drawn. Reset the deck to hand these out again.", DeckWording.AllDrawn(rumours));
    }

    [Fact]
    public void A_null_deck_is_a_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => DeckWording.Noun(null));
        Assert.Throws<ArgumentNullException>(() => DeckWording.Plural(null));
        Assert.Throws<ArgumentNullException>(() => DeckWording.DrawHint(null));
    }
}
