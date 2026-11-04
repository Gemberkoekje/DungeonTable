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
/// The real app on the sample pack: the DM's map says what is in each room beside its label, the Room
/// tab lists the rooms around the one on show with the same badges, and the projector shows none of it.
/// (The peek card is drawn on hover, and the labels' size on screen is the browser's; both were checked in
/// a browser.)
/// </summary>
public sealed class OnTheMapHostTests
{
    private const string GoblinDen = "data_dossiers_level_1_area_2";

    // Who the Goblin Den lists: Nib, the goblins by their stat block, and one of the crew's bugbears.
    private static readonly string[] DenCreatures = { "nib", "data_statblocks_monsters_goblin", "crew-bugbear" };

    [Fact]
    public async Task The_map_and_the_room_tab_say_what_is_in_each_room_and_the_projector_does_not()
    {
        using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = Client(factory);

        string dm = await client.GetStringAsync("/dm", CancellationToken.None);

        // On the map, under the Goblin Den's label: Nib, 1d4+2 goblins and a bugbear, its loot, and its
        // secret.
        string map = Between(dm, "class=\"editor-overlay\"", "class=\"dt-live\"");
        Assert.Contains(">Goblin Den</text><text class=\"dt-label dt-badges\"", map, StringComparison.Ordinal);
        Assert.Contains("☠\uFE0E 1d4+4</tspan>", map, StringComparison.Ordinal);

        // The screen opens on the Landing, whose ways out lead to the Goblin Den and the Nave.
        string nearby = Between(dm, "<section class=\"nearby\">", "</section>");
        Assert.Contains("Goblin Den", nearby, StringComparison.Ordinal);
        Assert.Contains("Nave", nearby, StringComparison.Ordinal);
        Assert.Contains("class=\"room-badge badge-creatures\"", nearby, StringComparison.Ordinal);
        Assert.DoesNotContain("room-connections", dm, StringComparison.Ordinal);

        // The player page carries the canvas's CSS, which names the badges' classes, so look for the
        // elements, not the words.
        string player = await client.GetStringAsync("/player", CancellationToken.None);
        Assert.DoesNotContain("class=\"dt-badge", player, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"dt-cleared\"", player, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"room-badge", player, StringComparison.Ordinal);
        Assert.DoesNotContain("1d4+4", player, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_room_whose_creatures_are_all_ticked_off_is_greyed_and_ticked_on_the_map()
    {
        using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = Client(factory);
        CampaignState campaign = factory.Services.GetRequiredService<CampaignState>();
        campaign.SetAreaCleared(GoblinDen, DenCreatures, cleared: true);

        string dm = await client.GetStringAsync("/dm", CancellationToken.None);

        string map = Between(dm, "class=\"editor-overlay\"", "class=\"dt-live\"");
        Assert.Contains("class=\"dt-cleared\"", map, StringComparison.Ordinal);
        Assert.Contains("✔\uFE0E</tspan>", map, StringComparison.Ordinal);
        Assert.DoesNotContain("1d4+4", map, StringComparison.Ordinal);
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
