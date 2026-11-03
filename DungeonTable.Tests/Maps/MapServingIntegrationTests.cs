using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DungeonTable.Tests.Maps;

/// <summary>
/// End-to-end checks over the running host: the hidden <c>*.regions.json</c> must never be
/// web-served, and both the DM and player views render the fully-vector map.
/// </summary>
public sealed class MapServingIntegrationTests : IClassFixture<MapServingFixture>
{
    private readonly MapServingFixture fixture;

    public MapServingIntegrationTests(MapServingFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Regions_json_is_not_web_served()
    {
        HttpResponseMessage response =
            await fixture.Client.GetAsync("/maps/adv/trap-map.regions.json", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Map_image_is_served_as_webp()
    {
        HttpResponseMessage response =
            await fixture.Client.GetAsync("/maps/adv/trap-map.webp", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("image/webp", response.Content.Headers.ContentType.MediaType);
    }

    [Fact]
    public async Task Dm_view_renders_the_vector_map_with_objects()
    {
        string html = await fixture.Client.GetStringAsync("/dm", CancellationToken.None);

        Assert.Contains("class=\"vector-map\"", html);
        Assert.Contains("data-object-kind=\"door\"", html);
    }

    [Fact]
    public async Task Dm_view_shows_the_map_and_the_room_on_one_screen()
    {
        string html = await fixture.Client.GetStringAsync("/dm", CancellationToken.None);

        // The map and the room it starts on are both on the page, not on two tabs of their own.
        Assert.Contains("class=\"vector-map\"", html, StringComparison.Ordinal);
        Assert.Contains("The stair from the chapel ends in a low stone room", html, StringComparison.Ordinal);

        // The dock's tabs, in their Ctrl+1..4 order; the old Map and Info tabs are gone.
        int[] tabs =
        {
            html.IndexOf("id=\"dm-tab-Room\"", StringComparison.Ordinal),
            html.IndexOf("id=\"dm-tab-Battle\"", StringComparison.Ordinal),
            html.IndexOf("id=\"dm-tab-Campaign\"", StringComparison.Ordinal),
            html.IndexOf("id=\"dm-tab-Art\"", StringComparison.Ordinal),
        };
        Assert.DoesNotContain(-1, tabs);
        Assert.Equal(tabs.OrderBy(index => index), tabs);
        Assert.DoesNotContain("dm-tab-Map", html, StringComparison.Ordinal);
        Assert.DoesNotContain("dm-tab-Info", html, StringComparison.Ordinal);

        // A click looks until a tool is picked.
        Assert.Matches("<button type=\"button\" class=\"map-tool active\" aria-pressed=\"true\"[^>]*>\\s*<span class=\"tool-glyph\" aria-hidden=\"true\">[^<]*</span>\\s*<span class=\"tool-label\">Look</span>", html);
    }

    [Fact]
    public async Task Player_view_renders_the_vector_map()
    {
        string html = await fixture.Client.GetStringAsync("/player", CancellationToken.None);

        Assert.Contains("class=\"vector-map\"", html);
        // No DM scaffolding reaches the player circuit: no tab shell, no briefing panel.
        Assert.DoesNotContain("dm-shell", html);
        Assert.DoesNotContain("briefing", html);
    }
}
