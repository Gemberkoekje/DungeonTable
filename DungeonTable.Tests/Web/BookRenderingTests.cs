using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Dossier;
using DungeonTable.Web.Components.Dm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The book index in the reference drawer: its own entries are badged as the book's, and what it
/// says under an id the campaign has its own card for is shown beneath that card, never instead.
/// </summary>
public sealed class BookRenderingTests
{
    private static readonly ReferenceCard FromTheBook = new ReferenceCard
    {
        NodeId = "wenna-brask",
        Kind = CrossRefKind.Book,
        Summary = "The miller, in the book's words.",
        Citation = "A Millbrook Almanac, p. 6",
    };

    [Fact]
    public async Task The_books_entry_extends_a_persons_card_beneath_it()
    {
        string html = await RenderDrawer(new Dictionary<string, object>
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard { NodeId = "wenna-brask", Label = "Wenna Brask", Kind = CrossRefKind.Npc },
            [nameof(ReferenceDrawer.Npc)] = new NpcDossier { Id = "wenna-brask", Name = "Wenna Brask" },
            [nameof(ReferenceDrawer.BookCard)] = FromTheBook,
        });

        Assert.Contains("In the book", html, StringComparison.Ordinal);
        Assert.Contains("The miller, in the book&#x27;s words.", html, StringComparison.Ordinal);
        Assert.Contains("A Millbrook Almanac, p. 6", html, StringComparison.Ordinal);
        Assert.True(html.IndexOf("npc-card", StringComparison.Ordinal) < html.IndexOf("In the book", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_card_the_book_has_nothing_under_shows_no_book_section()
    {
        string html = await RenderDrawer(new Dictionary<string, object>
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard { NodeId = "bell-warden", Label = "the Bell-Warden", Kind = CrossRefKind.Monster, Summary = "A gargoyle." },
        });

        Assert.DoesNotContain("In the book", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_book_entrys_own_card_is_headed_and_badged_as_the_books()
    {
        string html = await RenderDrawer(new Dictionary<string, object>
        {
            [nameof(ReferenceDrawer.Card)] = new ReferenceCard
            {
                NodeId = "the-old-mill",
                Label = "The Old Mill",
                Kind = CrossRefKind.Book,
                Summary = "The watermill by the ford.",
                Citation = "A Millbrook Almanac, p. 6",
            },
        });

        Assert.Contains("kind-book", html, StringComparison.Ordinal);
        Assert.Contains(">From the book</span>", html, StringComparison.Ordinal);
        Assert.Contains("Machine-made from the book", html, StringComparison.Ordinal);
        Assert.Contains("A Millbrook Almanac, p. 6", html, StringComparison.Ordinal);
    }

    private static async Task<string> RenderDrawer(Dictionary<string, object> parameters)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<ReferenceDrawer>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
