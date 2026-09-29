using DungeonTable.Application.Abstractions;
using DungeonTable.Core.Dossier;
using DungeonTable.Tests.Maps;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The composition root serves the link-aware resolver: the real host, over the sample pack,
/// resolves an authored link to the pack's stat block, book entry and citation.
/// </summary>
public sealed class LinkWiringTests
{
    [Fact]
    public void The_running_app_resolves_an_authored_link_against_its_content()
    {
        using WebApplicationFactory<Program> factory = Host();

        ICrossRefResolver resolver = factory.Services.GetRequiredService<ICrossRefResolver>();
        CrossRef link = Assert.Single(resolver.Resolve("Four [[goblin|Wickfoot goblins]] squabble over a candle."));

        Assert.Equal("data_statblocks_monsters_goblin", link.TargetId);
        Assert.Equal(CrossRefKind.Monster, link.Kind);
        Assert.Equal("Wickfoot goblins", link.Text);
    }

    [Fact]
    public void The_running_app_links_and_searches_the_book_index()
    {
        using WebApplicationFactory<Program> factory = Host();

        CrossRef link = Assert.Single(factory.Services.GetRequiredService<ICrossRefResolver>().Resolve("[[the-old-mill]] stands by the ford."));
        Assert.Equal(CrossRefKind.Book, link.Kind);
        Assert.Equal("The Old Mill", link.Text);

        SearchMatch hit = factory.Services.GetRequiredService<IContentProjection>().SearchNodes("The Old Mill", 5)[0];
        Assert.Equal("the-old-mill", hit.Id);
        Assert.Equal("book", hit.Category);
    }

    [Fact]
    public void The_running_app_links_only_what_an_author_marked()
    {
        using WebApplicationFactory<Program> factory = Host();

        ICrossRefResolver resolver = factory.Services.GetRequiredService<ICrossRefResolver>();

        // No name is guessed out of prose: a skeleton is only a link when someone wrote [[skeleton]].
        Assert.Empty(resolver.Resolve("A skeleton guards the door.", "data_dossiers_level_1"));
        Assert.Equal("data_statblocks_monsters_skeleton", Assert.Single(resolver.Resolve("A [[skeleton]] guards the door.", "data_dossiers_level_1")).TargetId);
    }

    [Fact]
    public void The_running_app_cites_a_stat_block_by_its_books_title()
    {
        using WebApplicationFactory<Program> factory = Host();

        ReferenceCard goblin = factory.Services.GetRequiredService<IContentProjection>().GetReferenceCard("data_statblocks_monsters_goblin").Value;

        // The block names SRD_CC_v5.1.pdf, and the pack's book index gives that file its title.
        Assert.StartsWith("SRD 5.1 p.", goblin.Citation, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Host() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
        });
}
