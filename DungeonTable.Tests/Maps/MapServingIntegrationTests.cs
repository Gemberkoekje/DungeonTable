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
    public async Task Player_view_renders_the_vector_map()
    {
        string html = await fixture.Client.GetStringAsync("/player", CancellationToken.None);

        Assert.Contains("class=\"vector-map\"", html);
        // No DM scaffolding reaches the player circuit: no tab shell, no briefing panel.
        Assert.DoesNotContain("dm-shell", html);
        Assert.DoesNotContain("briefing", html);
    }
}
