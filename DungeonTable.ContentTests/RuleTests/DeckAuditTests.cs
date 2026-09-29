using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>Proves the deck rules catch a deck that does not say how it is drawn, and a card that cannot be drawn or read.</summary>
public sealed class DeckAuditTests
{
    [Fact]
    public void A_deck_that_says_what_it_is_and_how_it_is_drawn_is_not_reported_and_needs_no_pages()
    {
        Assert.Empty(DeckAudit.Undescribed(Deck()));
        Assert.Empty(DeckAudit.BadCards(Deck()));
    }

    [Theory]
    [InlineData("id", "has no id, so the app skips it")]
    [InlineData("title", "has no title")]
    [InlineData("usage", "says nothing of when to draw from it")]
    [InlineData("rule", "says nothing of how it is drawn")]
    public void A_deck_that_leaves_something_unsaid_is_reported(string missing, string expected)
    {
        CardDeck deck = missing switch
        {
            "id" => Deck(id: string.Empty),
            "title" => Deck(title: string.Empty),
            "usage" => Deck(usage: string.Empty),
            _ => Deck(rule: DeckDrawRule.None),
        };

        string fault = Assert.Single(DeckAudit.Undescribed(deck));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_deck_document_with_the_same_id_is_reported_but_the_deck_itself_is_not()
    {
        CardDeck omens = Deck();

        string fault = Assert.Single(DeckAudit.WrittenTwice("deck-omens.json", omens, new[] { ("deck-omens.json", omens), ("deck-omens-2.json", Deck()), ("deck-rumours.json", Deck(id: "village-rumours")) }));

        Assert.Contains("deck-omens-2.json is the deck 'omens-of-the-bell' too", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_with_no_id_no_name_nothing_to_read_or_a_shared_id_is_reported()
    {
        CardDeck deck = Deck(cards: new[]
        {
            new DeckCard { Name = "Cracked Bell", Body = "A far-off note." },
            new DeckCard { Id = "silent", Body = "Nothing." },
            new DeckCard { Id = "guttering-candle", Name = "Guttering Candle" },
            new DeckCard { Id = "rope", Name = "Rope Without a Bell", Effects = new[] { new DossierBlock { Heading = "Bane Effect", Body = "Silence." } } },
            new DeckCard { Id = "rope", Name = "Rope Again", Body = "Again." },
        });

        IReadOnlyList<string> faults = DeckAudit.BadCards(deck);

        Assert.Equal(4, faults.Count);
        Assert.Contains("cards[0], has no id", faults[0], StringComparison.Ordinal);
        Assert.Contains("card 'silent', has no name", faults[1], StringComparison.Ordinal);
        Assert.Contains("card 'guttering-candle', has nothing to read out", faults[2], StringComparison.Ordinal);
        Assert.Contains("two cards with the id 'rope'", faults[3], StringComparison.Ordinal);
    }

    private static CardDeck Deck(
        string id = "omens-of-the-bell",
        string title = "Omens of the Bell",
        string usage = "Draw one as the party enters the nave.",
        DeckDrawRule rule = DeckDrawRule.Fresh,
        IReadOnlyList<DeckCard> cards = null) => new()
    {
        Id = id,
        Title = title,
        Usage = usage,
        DrawRule = rule,
        Cards = cards ?? new[] { new DeckCard { Id = "cracked-bell", Name = "Cracked Bell", Body = "A far-off note." } },
    };
}
