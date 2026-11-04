using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Session;
using DungeonTable.Core.Stats;
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
/// The encounter strip: with nothing in it, it says why (the area lists nobody, so nobody is there);
/// with creatures counted down, dealt with elsewhere or fled, each row says so.
/// </summary>
public sealed class EncounterStripRenderingTests
{
    [Fact]
    public async Task An_area_that_lists_nobody_says_so()
    {
        string html = await Render();

        Assert.Contains("Nobody is listed in this area.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("named in this area", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_somebody_dealt_with_in_another_room_says_where_and_a_band_says_what_is_left()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.Count("area-workshop", "grask", 1, 0);
        campaign.Count("area-den", "crew-bugbear", 1, 0);
        campaign.Count(Vestry, "crew-bugbear", 1, 0);
        EncounterMonster[] rows =
        {
            new EncounterMonster { NodeId = "grask", Name = "Grask", Individual = true, Shared = true },
            new EncounterMonster { NodeId = "crew-bugbear", Name = "Crew bugbear", SuggestedCount = 3, Shared = true, BandSize = 5 },
            new EncounterMonster { NodeId = "bugbear", Name = "Bugbear", SuggestedCount = 2 },
        };

        string html = await Render(rows, campaign);

        // Grask: gone, and the chip says where, by the area's name without its key.
        Assert.Contains("dealt with in The Workshop<", html, StringComparison.Ordinal);

        // The crew: one dealt with here, one in the den, so two of the three listed here are left, and
        // three of the band's five.
        Assert.Contains("2 of 3 left", html, StringComparison.Ordinal);
        Assert.Contains("3 of 5 in all", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"One Crew bugbear back\"", html, StringComparison.Ordinal);
        Assert.Contains("one fled", html, StringComparison.Ordinal);

        // A kind of creature counts down but never flees: it is not listed anywhere else as itself.
        Assert.Contains("aria-label=\"One Bugbear dealt with\"", html, StringComparison.Ordinal);
        Assert.Contains("1 of 3 dealt with", html, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(html, "one fled"));
        Assert.Equal(1, Occurrences(html, "class=\"tally-flee\""));
    }

    [Fact]
    public async Task One_somebody_who_fled_is_ticked_off_here_and_can_be_brought_back()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.Count(Vestry, "grask", 0, 1);
        EncounterMonster[] rows = { new EncounterMonster { NodeId = "grask", Name = "Grask", Individual = true, Shared = true } };

        string html = await Render(rows, campaign);

        Assert.Contains("class=\"encounter-row cleared\"", html, StringComparison.Ordinal);
        Assert.Contains("class=\"tally-fled on\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"tally-flee\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dealt with in", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_creature_moved_here_says_where_from_and_one_moved_away_says_where_it_went()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.Move(new CreatureMove { CreatureId = "golem", FromAreaNodeId = "area-workshop", ToAreaNodeId = Vestry, Count = 1 }, 0, oneSomebody: true);
        campaign.Move(new CreatureMove { CreatureId = "bugbear", FromAreaNodeId = Vestry, ToAreaNodeId = "area-workshop", Count = 2 }, 0, oneSomebody: false);
        EncounterMonster[] rows =
        {
            new EncounterMonster { NodeId = "golem", Name = "Clay golem", Individual = true, Shared = true }.Moved(1),
            new EncounterMonster { NodeId = "bugbear", Name = "Bugbear", SuggestedCount = 3 },
        };

        string html = await Render(rows, campaign, onMove: (_, _, _) => { });

        Assert.Contains("moved here from The Workshop", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Send Clay golem back to The Workshop (Level 1, Area 4)\"", html, StringComparison.Ordinal);
        Assert.Contains("2 moved to The Workshop", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Bring Bugbear back here\"", html, StringComparison.Ordinal);

        // The one bugbear left still counts down and can still move; the golem is here and up.
        Assert.Equal(2, Occurrences(html, "class=\"encounter-move\""));
        Assert.DoesNotContain("class=\"encounter-row cleared\"", html, StringComparison.Ordinal);
        Assert.Contains("0 of 2 dealt with", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_somebody_moved_elsewhere_is_gone_from_here_offers_no_move_and_says_where()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.Move(new CreatureMove { CreatureId = "grask", FromAreaNodeId = Vestry, ToAreaNodeId = "area-workshop", Count = 1 }, 0, oneSomebody: true);
        EncounterMonster[] rows = { new EncounterMonster { NodeId = "grask", Name = "Grask", Individual = true, Shared = true } };

        string html = await Render(rows, campaign, onMove: (_, _, _) => { });

        Assert.Contains("class=\"encounter-row cleared\"", html, StringComparison.Ordinal);
        Assert.Contains("moved to The Workshop", html, StringComparison.Ordinal);
        Assert.Contains("Moved to another room. Untick to bring them back here", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"encounter-move\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"encounter-status\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_strip_that_cannot_move_offers_no_move()
    {
        EncounterMonster[] rows = { new EncounterMonster { NodeId = "bugbear", Name = "Bugbear", SuggestedCount = 3 } };

        string html = await Render(rows, new CampaignState(new FakePartyRoster()));

        Assert.DoesNotContain("class=\"encounter-move\"", html, StringComparison.Ordinal);
    }

    private const string Vestry = "area-vestry";

    private static int Occurrences(string text, string part)
    {
        int count = 0;
        for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static Task<string> Render() =>
        Render(Array.Empty<EncounterMonster>(), new CampaignState(new FakePartyRoster()), "data_dossiers_level_1_area_24");

    private static async Task<string> Render(EncounterMonster[] rows, CampaignState campaign, string area = Vestry, Action<EncounterMonster, string, int> onMove = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BattleState(new FakePartyRoster(), new FakeStatLibrary()));
        services.AddSingleton(campaign);
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        Func<string, string> label = id => id == "area-workshop" ? "The Workshop (Level 1, Area 4)" : id;
        var parameters = new Dictionary<string, object>
        {
            [nameof(EncounterStrip.Monsters)] = rows,
            [nameof(EncounterStrip.AreaNodeId)] = area,
            [nameof(EncounterStrip.AreaLabel)] = label,
        };
        if (onMove is not null)
        {
            parameters[nameof(EncounterStrip.OnMove)] = onMove;
        }

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<EncounterStrip>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
