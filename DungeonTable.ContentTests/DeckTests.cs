using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests;

/// <summary>Every deck says what it is and how it is drawn, and every card can be drawn and read out.</summary>
public sealed class DeckTests
{
    public static TheoryData<string> Decks => ContentDocuments.Names(DocumentKind.Deck);

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Decks))]
    public void Every_deck_says_what_it_is_and_how_it_is_drawn(string document)
    {
        CardDeck deck = ContentDocuments.Loaded<CardDeck>(document);

        Report.None(DeckAudit.Undescribed(deck), $"What {document} leaves unsaid");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Decks))]
    public void No_two_decks_share_an_id(string document)
    {
        CardDeck deck = ContentDocuments.Loaded<CardDeck>(document);

        Report.None(
            DeckAudit.WrittenTwice(document, deck, ContentDocuments.LoadedOf<CardDeck>(DocumentKind.Deck)),
            $"Decks sharing {document}'s id");
    }

    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(Decks))]
    public void Every_card_can_be_drawn_and_read_out(string document)
    {
        CardDeck deck = ContentDocuments.Loaded<CardDeck>(document);

        Report.None(DeckAudit.BadCards(deck), $"Cards in {document}");
    }
}
