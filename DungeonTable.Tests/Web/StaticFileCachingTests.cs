using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The app's own scripts are linked without a fingerprint, so a browser must ask before reusing its
/// copy: otherwise, after a deploy, it can run the last version's JavaScript against this version's
/// markup.
/// </summary>
public sealed class StaticFileCachingTests : IClassFixture<MapServingFixture>
{
    private readonly MapServingFixture fixture;

    public StaticFileCachingTests(MapServingFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task A_script_is_revalidated_before_a_browser_reuses_it()
    {
        HttpResponseMessage response = await fixture.Client.GetAsync("/js/dt-editor.js", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl.NoCache);
    }

    [Fact]
    public async Task A_map_image_keeps_its_own_caching()
    {
        // /maps has static-file options of its own; only the app's own files changed.
        HttpResponseMessage response = await fixture.Client.GetAsync("/maps/adv/trap-map.webp", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.CacheControl?.NoCache ?? false);
    }
}
