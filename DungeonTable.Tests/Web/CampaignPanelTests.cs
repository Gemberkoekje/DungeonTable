using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Components.Dm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders the Campaign tab's sections to HTML: which floor their prose is read on, and the words the
/// campaign itself chose for them.
/// </summary>
public sealed class CampaignPanelTests
{
    [Fact]
    public async Task The_campaign_tab_opens_on_the_level_and_offers_every_section()
    {
        string html = await Render(new Dictionary<string, object>
        {
            [nameof(CampaignPanel.Level)] = new LevelDossier { Title = "Level 1: The Undercroft" },
        });

        Assert.Contains("<h2 class=\"level-title\">Level 1: The Undercroft</h2>", html, StringComparison.Ordinal);
        foreach (string label in new[] { "Level", "Quests", "Party", "NPCs", "Story", "Session", "Rules", "Search" })
        {
            Assert.Contains($"id=\"campaign-tab-{(label == "NPCs" ? "Npcs" : label)}\"", html, StringComparison.Ordinal);
        }

        // The room is the Room tab's: the Campaign tab has no Room section of its own.
        Assert.DoesNotContain("campaign-tab-Room", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Level")]
    [InlineData("Rules")]
    public async Task Campaign_and_rules_prose_is_read_on_no_floor(string tab)
    {
        // campaign.json and reference.json belong to no floor. The content tests and the format read
        // them on none, so a bare [[area 3]] there is broken; the DM screen read them on the floor on
        // show, where the same link worked on one floor and pointed elsewhere on the next.
        const string Prose = "Take the ringers' stair down.";
        var onTheShownFloor = new ScriptedCrossRefResolver().Add("the ringers' stair", "data_dossiers_level_1_area_5", CrossRefKind.Area);
        var onNoFloor = new ScriptedCrossRefResolver();
        var block = new DossierBlock { Heading = "The way down", Body = Prose };

        string html = await Render(new Dictionary<string, object>
        {
            [nameof(CampaignPanel.InitialTab)] = tab,
            [nameof(CampaignPanel.Campaign)] = new CampaignDossier { Title = "The Silent Bell", Sections = new[] { block } },
            [nameof(CampaignPanel.Reference)] = new ReferenceLibrary { Sections = new[] { block } },
            [nameof(CampaignPanel.ResolveRefs)] = (Func<string, IReadOnlyList<CrossRef>>)(text => onTheShownFloor.Resolve(text, "data_dossiers_level_1")),
            [nameof(CampaignPanel.ResolveEntityRefs)] = (Func<string, IReadOnlyList<CrossRef>>)onNoFloor.Resolve,
        });

        Assert.Contains("<div class=\"detail-body\">Take the ringers&#x27; stair down.</div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("xref-area", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rules_tab_is_headed_in_the_campaigns_own_words_or_not_at_all()
    {
        // It said "Dungeon features" over every campaign's rules: one book's wording, left in the app.
        var sections = new[] { new DossierBlock { Heading = "Darkness Below", Body = "Count the lanterns." } };

        string titled = await Render(new Dictionary<string, object>
        {
            [nameof(CampaignPanel.InitialTab)] = "Rules",
            [nameof(CampaignPanel.Reference)] = new ReferenceLibrary { Title = "Rules of the undercroft", Sections = sections },
        });
        string untitled = await Render(new Dictionary<string, object>
        {
            [nameof(CampaignPanel.InitialTab)] = "Rules",
            [nameof(CampaignPanel.Reference)] = new ReferenceLibrary { Sections = sections },
        });

        Assert.Contains("<div class=\"detail\"><h3>Rules of the undercroft</h3><details", titled, StringComparison.Ordinal);
        Assert.Contains("<div class=\"detail\"><details", untitled, StringComparison.Ordinal);
        Assert.DoesNotContain("Dungeon features", titled + untitled, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> Render(Dictionary<string, object> parameters)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<CampaignPanel>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
