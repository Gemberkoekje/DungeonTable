using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The real app on the sample pack: the bar offers Find anything, the Room tab's header the floor's ⓘ, the
/// floor's facts on the Campaign tab carry pins, and what the table pinned sits along the foot of the dock.
/// (The box itself, the sheet, the popovers and the keys are drawn and driven in a browser; they were
/// checked there.)
/// </summary>
public sealed class AnswersHostTests
{
    [Fact]
    public async Task The_dm_screen_offers_find_anything_the_floors_facts_and_the_tables_pins()
    {
        using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = Client(factory);
        CampaignState campaign = factory.Services.GetRequiredService<CampaignState>();
        campaign.TogglePin(PinKeys.Passage("rules/darkness-below"));
        campaign.TogglePin(PinKeys.Card("grask"));
        campaign.TogglePin(PinKeys.Passage("rules/a-section-since-renamed"));

        string dm = await client.GetStringAsync("/dm", CancellationToken.None);

        string bar = Between(dm, "class=\"dm-player-bar\"", "class=\"dm-body\"");
        Assert.Contains("class=\"find-open-btn\"", bar, StringComparison.Ordinal);

        // The room's header has the floor's ⓘ; the facts open under it on a click, so they are not drawn yet.
        string head = Between(dm, "<header class=\"room-head\">", "</header>");
        Assert.Contains("room-action level-info", head, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"room-level\"", dm, StringComparison.Ordinal);

        // The Campaign tab opens on the floor, whose facts each carry a pin; it has no Search any more.
        string campaignTab = Between(dm, "id=\"dm-panel-Campaign\"", "id=\"dm-panel-Art\"");
        Assert.Contains("class=\"pin-btn", campaignTab, StringComparison.Ordinal);
        Assert.DoesNotContain("campaign-tab-Search", campaignTab, StringComparison.Ordinal);

        // The pins, in the order they were pinned, by their headings and names; one the content no longer
        // holds stays, greyed, by the last part of its key.
        string strip = Between(dm, "class=\"pin-strip\"", "</aside>");
        int darkness = strip.IndexOf(">Darkness Below</button>", StringComparison.Ordinal);
        int grask = strip.IndexOf(">Grask</button>", StringComparison.Ordinal);
        Assert.True(darkness >= 0 && grask > darkness, strip);
        Assert.Contains("pin-chip  gone", strip, StringComparison.Ordinal);
        Assert.Contains(">a-section-since-renamed</button>", strip, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_nothing_pinned_there_is_no_strip()
    {
        using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = Client(factory);

        string dm = await client.GetStringAsync("/dm", CancellationToken.None);

        Assert.DoesNotContain("class=\"pin-strip\"", dm, StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
        });

    private static HttpClient Client(WebApplicationFactory<Program> factory)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));
        return client;
    }

    private static string Between(string html, string start, string end)
    {
        int from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"No {start} on the page.");
        int to = html.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(to > from, $"No {end} after {start} on the page.");
        return html[from..to];
    }
}
