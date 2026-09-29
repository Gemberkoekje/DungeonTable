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
/// Renders the Level tab's level table to HTML: the campaign names it and may say what it is for,
/// and the app supplies nothing of its own beyond a plain "Levels".
/// </summary>
public sealed class LevelTableTests
{
    private static readonly LevelSummary[] TwoLevels =
    {
        new LevelSummary { Level = 1, Name = "The Undercroft", CharacterLevel = "2nd", Authored = true },
        new LevelSummary { Level = 2, Name = "The Sealed Crypt", CharacterLevel = "3rd" },
    };

    [Fact]
    public async Task The_table_carries_the_campaigns_own_heading_and_note()
    {
        string html = await Render(new CampaignDossier
        {
            LevelsHeading = "Levels of the Undercroft",
            LevelsNote = "the crypt stays sealed until the bell rings",
            Levels = TwoLevels,
        });

        Assert.Contains(
            "<span class=\"detail-heading\">Levels of the Undercroft</span>"
            + "<span class=\"ref\">the crypt stays sealed until the bell rings</span></summary>",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_campaign_that_names_nothing_gets_a_plain_heading_and_no_note()
    {
        string html = await Render(new CampaignDossier { LevelsHeading = "   ", LevelsNote = " ", Levels = TwoLevels });

        Assert.Contains("<span class=\"detail-heading\">Levels</span></summary>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<span class=\"ref\"> </span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Each_level_is_a_row_and_the_authored_ones_are_marked()
    {
        string html = await Render(new CampaignDossier { Levels = TwoLevels });

        Assert.Contains("<tr class=\"authored\"><td>1</td>", html, StringComparison.Ordinal);
        Assert.Contains("The Undercroft", html, StringComparison.Ordinal);
        Assert.Contains("<td>2nd</td>", html, StringComparison.Ordinal);
        Assert.Contains("<tr class=\"\"><td>2</td>", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("in app").Length - 1);
    }

    [Fact]
    public async Task No_levels_means_no_table()
    {
        string html = await Render(new CampaignDossier { LevelsHeading = "Levels of the Undercroft" });

        Assert.Equal(string.Empty, html.Trim());
    }

    private static async Task<string> Render(CampaignDossier campaign)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(LevelTable.Campaign)] = campaign,
            });

            HtmlRootComponent output = await renderer.RenderComponentAsync<LevelTable>(parameters);
            return output.ToHtmlString();
        });
    }
}
