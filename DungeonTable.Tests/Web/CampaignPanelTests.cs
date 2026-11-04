using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
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
        foreach (string label in new[] { "Level", "Quests", "Party", "NPCs", "Story", "Session", "Rules" })
        {
            Assert.Contains($"id=\"campaign-tab-{(label == "NPCs" ? "Npcs" : label)}\"", html, StringComparison.Ordinal);
        }

        // The room is the Room tab's, and searching is Find anything's, over the map: the Campaign tab has
        // a section for neither.
        Assert.DoesNotContain("campaign-tab-Room", html, StringComparison.Ordinal);
        Assert.DoesNotContain("campaign-tab-Search", html, StringComparison.Ordinal);
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

        Assert.Contains("<div class=\"detail\"><h3>Rules of the undercroft</h3><div class=\"pinnable\"><details", titled, StringComparison.Ordinal);
        Assert.Contains("<div class=\"detail\"><div class=\"pinnable\"><details", untitled, StringComparison.Ordinal);
        Assert.DoesNotContain("Dungeon features", titled + untitled, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Each_rules_section_floor_fact_and_party_sheet_carries_a_pin_naming_it()
    {
        // The pins name the passages the text index files these blocks under; a lit pin is one the table
        // already holds, which is how a key that drifted from the index would show.
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.TogglePin("text:rules/darkness-below");
        campaign.TogglePin("text:level/data_dossiers_level_1/factions/grasks-crew");
        var rules = new ReferenceLibrary
        {
            Sections = new[]
            {
                new DossierBlock { Heading = "Darkness Below", Body = "Count the lanterns." },
                new DossierBlock { Heading = "Resting", Body = "Short rests are safe." },
            },
        };
        var level = new LevelDossier
        {
            LevelNodeId = "data_dossiers_level_1",
            Title = "Level 1: The Undercroft",
            WhatDwellsHere = "Goblins, bugbears and a gargoyle.",
            Factions = new[] { new DossierBlock { Id = "grasks-crew", Heading = "The Bugbears", Body = "Three of them." } },
            WanderingMonsters = new[] { new DossierBlock { Heading = "Fetchers", Body = "Two goblins." } },
        };

        string rulesTab = await Render(
            new Dictionary<string, object>
            {
                [nameof(CampaignPanel.InitialTab)] = "Rules",
                [nameof(CampaignPanel.Reference)] = rules,
                [nameof(CampaignPanel.Pinnable)] = true,
            },
            campaign);
        string levelTab = await Render(
            new Dictionary<string, object>
            {
                [nameof(CampaignPanel.Level)] = level,
                [nameof(CampaignPanel.Pinnable)] = true,
            },
            campaign);
        campaign.TogglePin("card:maren");
        string partyTab = await Render(
            new Dictionary<string, object>
            {
                [nameof(CampaignPanel.InitialTab)] = "Party",
                [nameof(CampaignPanel.Party)] = new PartyDossier
                {
                    Characters = new[]
                    {
                        new CharacterDossier { Id = "maren", Name = "Maren" },
                        new CharacterDossier { Id = "oswin", Name = "Oswin" },
                    },
                },
                [nameof(CampaignPanel.Pinnable)] = true,
            },
            campaign);
        string unpinnable = await Render(new Dictionary<string, object>
        {
            [nameof(CampaignPanel.InitialTab)] = "Rules",
            [nameof(CampaignPanel.Reference)] = rules,
        });

        Assert.Equal(2, Count(rulesTab, "class=\"pin-btn"));
        Assert.Equal(1, Count(rulesTab, "class=\"pin-btn on\""));
        Assert.True(rulesTab.IndexOf("class=\"pin-btn on\"", StringComparison.Ordinal) < rulesTab.IndexOf("Resting", StringComparison.Ordinal));

        // What dwells here, the one faction (lit, by its id rather than its heading) and the one wanderer.
        Assert.Equal(3, Count(levelTab, "class=\"pin-btn"));
        Assert.Equal(1, Count(levelTab, "class=\"pin-btn on\""));

        // A player character's sheet, pinned as the card a link to them opens.
        Assert.Equal(2, Count(partyTab, "class=\"pin-btn"));
        Assert.Equal(1, Count(partyTab, "class=\"pin-btn on\""));

        Assert.DoesNotContain("pin-btn", unpinnable, StringComparison.Ordinal);
    }

    private static int Count(string html, string needle)
    {
        int count = 0;
        for (int at = html.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = html.IndexOf(needle, at + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static async Task<string> Render(Dictionary<string, object> parameters, CampaignState campaign)
    {
        await using ServiceProvider services = new ServiceCollection().AddSingleton(campaign).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<CampaignPanel>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
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
