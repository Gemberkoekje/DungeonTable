using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Outside Development an unhandled error is re-executed to <c>/error</c>. Nothing answered there, so a
/// crash reached the browser as an empty 500 with no word of what to do next.
/// </summary>
public sealed class ErrorPageTests
{
    [Fact]
    public async Task A_page_that_fails_in_production_answers_with_the_error_page()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseEnvironment("Production");
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);

            // The DM screen cannot be built: the page fails as it renders.
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DmWorkspace>();
                services.AddScoped<DmWorkspace>(_ => throw new InvalidOperationException("The DM screen could not be built."));
            });
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));

        HttpResponseMessage response = await client.GetAsync("/dm", CancellationToken.None);
        string body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("<h1>Something went wrong</h1>", body, StringComparison.Ordinal);
        Assert.Contains("<a href=\"dm\">Back to the DM screen</a>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be built", body, StringComparison.Ordinal);
    }
}
