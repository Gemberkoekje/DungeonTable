using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace DungeonTable.Tests.Art;

/// <summary>
/// End-to-end checks over the running host for the <c>/art</c> route: it is behind the shared
/// passphrase like everything else, it serves raster art, and it refuses everything else in the
/// art directory — the catalogue document, and above all an SVG, which at
/// <c>/art/x.svg</c> would be same-origin markup that can run script.
/// </summary>
public sealed class ArtRouteTests : IClassFixture<ArtServingFixture>
{
    private readonly ArtServingFixture fixture;

    public ArtRouteTests(ArtServingFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Art_is_served_to_an_authenticated_request()
    {
        HttpResponseMessage response =
            await fixture.Client.GetAsync("/art/mm/sample.webp", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Art_is_behind_the_passphrase_gate()
    {
        // The gate is the whole reason copyrighted book art can be served at a public URL at all
        // (CLAUDE.md). An exemption for /art would undo it.
        using HttpClient anonymous = fixture.CreateUnauthenticatedClient();

        HttpResponseMessage response =
            await anonymous.GetAsync("/art/mm/sample.webp", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/art/evil.svg")]
    [InlineData("/art/catalogue.json")]
    [InlineData("/art/notes.txt")]
    public async Task Anything_that_is_not_raster_art_is_refused(string path)
    {
        HttpResponseMessage response = await fixture.Client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>
/// Boots the real web host against a throwaway art root holding one servable picture and the three
/// kinds of file the route must refuse.
/// </summary>
public sealed class ArtServingFixture : IDisposable
{
    private const string Passphrase = "test-passphrase";

    private readonly string tempArt =
        Path.Combine(Path.GetTempPath(), "dt-art-route-" + Guid.NewGuid().ToString("N"));

    private readonly WebApplicationFactory<Program> factory;

    public ArtServingFixture()
    {
        Directory.CreateDirectory(Path.Combine(tempArt, "mm"));

        // Static serving keys the content type off the extension, so a minimal RIFF/WEBP header is
        // all the bytes have to be.
        File.WriteAllBytes(
            Path.Combine(tempArt, "mm", "sample.webp"),
            new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 });

        File.WriteAllText(
            Path.Combine(tempArt, "evil.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");
        File.WriteAllText(Path.Combine(tempArt, "catalogue.json"), """{ "images": [] }""");
        File.WriteAllText(Path.Combine(tempArt, "notes.txt"), "not art");

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Art:Root", tempArt);
            builder.UseSetting("Auth:Passphrase", Passphrase);
        });

        Client = factory.CreateClient();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{Passphrase}")));
    }

    /// <summary>An HTTP client against the running test host, pre-authenticated.</summary>
    public HttpClient Client { get; }

    /// <summary>A client against the same host that never sends credentials.</summary>
    public HttpClient CreateUnauthenticatedClient() => factory.CreateClient();

    public void Dispose()
    {
        Client.Dispose();
        factory.Dispose();
        if (Directory.Exists(tempArt))
        {
            Directory.Delete(tempArt, recursive: true);
        }
    }
}
