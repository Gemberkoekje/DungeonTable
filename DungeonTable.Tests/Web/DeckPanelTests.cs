using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Session;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Components.Dm;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders one deck on the Rules tab to HTML. Everything the panel does differently for a deck comes
/// from the deck document: whether it counts what is left and can be reset (its draw rule), and what
/// it calls its cards.
/// </summary>
public sealed partial class DeckPanelTests
{
    private const string Rumours = "village-rumours";

    private static readonly CardDeck KeptRumours = new CardDeck
    {
        Id = Rumours,
        Title = "Village Rumours",
        CardNoun = "rumour",
        DrawRule = DeckDrawRule.Kept,
        Usage = "Draw one when the party asks around the village.",
        Cards = new[]
        {
            new DeckCard { Id = "the-millers-debt", Name = "The Millers Debt", Body = "He owes more than he says." },
            new DeckCard { Id = "the-sextons-key", Name = "The Sextons Key", Body = "It opens more than the chapel." },
        },
    };

    [Fact]
    public async Task A_kept_deck_counts_what_is_left_marks_what_is_drawn_and_can_be_reset()
    {
        string html = await Render(KeptRumours, Drawn(Rumours, "the-millers-debt"));

        Assert.Contains("<span class=\"deck-count\" title=\"Rumours still in the deck\">1 / 2 left</span>", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Put every drawn rumour back in the deck\"", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Draw one of the rumours not handed out yet\"", html, StringComparison.Ordinal);
        Assert.Contains("All 2 rumours", html, StringComparison.Ordinal);
        Assert.Contains("Draw one when the party asks around the village.", html, StringComparison.Ordinal);

        // Only the drawn card carries the badge.
        Assert.Equal(1, html.Split("class=\"deck-spent\"").Length - 1);
        Assert.Contains("The Millers Debt</span><span class=\"deck-spent\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", DrawButton(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_kept_deck_with_every_card_dealt_says_so_and_cannot_draw()
    {
        string html = await Render(KeptRumours, Drawn(Rumours, "the-millers-debt", "the-sextons-key"));

        Assert.Contains("0 / 2 left", html, StringComparison.Ordinal);
        Assert.Contains("Every rumour has been drawn. Reset the deck to hand these out again.", html, StringComparison.Ordinal);
        Assert.Contains("disabled", DrawButton(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_fresh_deck_has_no_count_no_reset_and_nothing_struck_off()
    {
        // Even a card recorded as drawn (from when the deck was kept, say) is back in a fresh deck.
        var omens = new CardDeck
        {
            Id = Rumours,
            Title = "Omens of the Bell",
            CardNoun = "omen",
            DrawRule = DeckDrawRule.Fresh,
            Cards = new[] { new DeckCard { Id = "the-millers-debt", Name = "Cracked Bell" } },
        };

        string html = await Render(omens, Drawn(Rumours, "the-millers-debt"));

        Assert.DoesNotContain("deck-count", html, StringComparison.Ordinal);
        Assert.DoesNotContain("deck-reset", html, StringComparison.Ordinal);
        Assert.DoesNotContain("deck-spent", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Draw a random omen\"", html, StringComparison.Ordinal);
        Assert.Contains("All 1 omen", html, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", DrawButton(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_deck_that_writes_its_own_hint_shows_it_on_the_draw_button()
    {
        var deck = new CardDeck
        {
            Id = "omens",
            Title = "Omens of the Bell",
            DrawHint = "Read one aloud when the bell is struck",
            Cards = new[] { new DeckCard { Id = "cracked-bell", Name = "Cracked Bell" } },
        };

        string html = await Render(deck, new CampaignSnapshot());

        Assert.Contains("title=\"Read one aloud when the bell is struck\"", DrawButton(html), StringComparison.Ordinal);
    }

    // The Draw button's opening tag.
    private static string DrawButton(string html) => DrawButtonTag().Match(html).Value;

    [GeneratedRegex("<button[^>]*class=\"deck-draw\"[^>]*>")]
    private static partial Regex DrawButtonTag();

    private static CampaignSnapshot Drawn(string deckId, params string[] cardIds) => new CampaignSnapshot
    {
        Decks = new[] { new DeckProgress { DeckId = deckId, DrawnCardIds = cardIds } },
    };

    private static async Task<string> Render(CardDeck deck, CampaignSnapshot table)
    {
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.Restore(table);

        await using ServiceProvider services = new ServiceCollection().AddSingleton(campaign).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(DeckPanel.Deck)] = deck,
            });

            HtmlRootComponent output = await renderer.RenderComponentAsync<DeckPanel>(parameters);
            return output.ToHtmlString();
        });
    }
}
