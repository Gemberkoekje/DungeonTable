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
/// The whole app is served at a public URL, so every route must sit behind the shared
/// passphrase — this exercises the real running host, not just the middleware class in isolation.
/// </summary>
public sealed class PassphraseAuthTests : IClassFixture<MapServingFixture>
{
    private readonly MapServingFixture fixture;

    public PassphraseAuthTests(MapServingFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Request_without_credentials_is_rejected()
    {
        using HttpClient client = fixture.CreateUnauthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(response.Headers.WwwAuthenticate.FirstOrDefault());
    }

    [Fact]
    public async Task Request_with_wrong_passphrase_is_rejected()
    {
        using HttpClient client = fixture.CreateUnauthenticatedClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(":not-the-passphrase")));

        HttpResponseMessage response = await client.GetAsync("/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_correct_passphrase_is_accepted()
    {
        HttpResponseMessage response = await fixture.Client.GetAsync("/dm", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Healthz_is_exempt_from_the_passphrase_gate()
    {
        using HttpClient client = fixture.CreateUnauthenticatedClient();

        HttpResponseMessage response = await client.GetAsync("/healthz", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_passphrase_stops_the_app_from_starting(string blank)
    {
        // appsettings.json ships the slot as "", so a deployment that loses Auth__Passphrase reads an
        // empty string rather than null. Started anyway, the gate lets in an empty password.
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", blank);
        });

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Auth:Passphrase", error.Message, StringComparison.Ordinal);
    }
}
