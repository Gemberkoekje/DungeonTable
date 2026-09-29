using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The <c>PathBase</c> setting: the prefix a shared host serves the app under, alongside the root of
/// the app's own host. It is a deployment value, so the code has no default for it; the deployed
/// image sets it.
/// </summary>
public sealed class PathBaseTests : IClassFixture<MapServingFixture>, IClassFixture<PathBaseFixture>
{
    private readonly MapServingFixture unprefixed;
    private readonly PathBaseFixture prefixed;

    public PathBaseTests(MapServingFixture unprefixed, PathBaseFixture prefixed)
    {
        this.unprefixed = unprefixed;
        this.prefixed = prefixed;
    }

    [Fact]
    public async Task Without_a_path_base_the_app_is_served_at_the_root_only()
    {
        HttpResponseMessage root = await unprefixed.Client.GetAsync("/dm", CancellationToken.None);
        HttpResponseMessage prefixedPath = await unprefixed.Client.GetAsync("/dnd/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("<base href=\"/\"", await root.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, prefixedPath.StatusCode);
    }

    [Fact]
    public async Task A_path_base_serves_the_app_under_it_and_points_its_urls_there()
    {
        HttpResponseMessage response = await prefixed.Client.GetAsync("/dnd/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<base href=\"/dnd/\"", await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_path_base_leaves_the_root_entry_point_working()
    {
        HttpResponseMessage response = await prefixed.Client.GetAsync("/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<base href=\"/\"", await response.Content.ReadAsStringAsync(CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_liveness_probe_stays_ungated_under_the_path_base()
    {
        using HttpClient anonymous = prefixed.CreateUnauthenticatedClient();

        HttpResponseMessage response = await anonymous.GetAsync("/dnd/healthz", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void A_path_base_without_a_leading_slash_stops_the_app_from_starting()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
            builder.UseSetting("PathBase", "dnd");
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("PathBase", error.Message, StringComparison.Ordinal);
    }
}

/// <summary>Boots the real web host with <c>PathBase</c> set to <c>/dnd</c>, as the deployed image does.</summary>
public sealed class PathBaseFixture : IDisposable
{
    private readonly WebApplicationFactory<Program> factory;

    public PathBaseFixture()
    {
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
            builder.UseSetting("PathBase", "/dnd");
        });

        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));
    }

    /// <summary>An HTTP client against the running test host, pre-authenticated.</summary>
    public HttpClient Client { get; }

    /// <summary>A client against the same host that never sends credentials.</summary>
    public HttpClient CreateUnauthenticatedClient() => factory.CreateClient();

    public void Dispose()
    {
        Client.Dispose();
        factory.Dispose();
    }
}
