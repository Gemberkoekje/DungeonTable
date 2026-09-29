using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Every stylesheet and script a page links is one the app serves. A link to a file that is never
/// built, such as the bundle Blazor makes only when a component has a <c>.razor.css</c>, answers 404
/// on every page load, and nothing else notices.
/// </summary>
public sealed partial class LinkedAssetsTests : IClassFixture<MapServingFixture>
{
    private readonly MapServingFixture fixture;

    public LinkedAssetsTests(MapServingFixture fixture) => this.fixture = fixture;

    [Theory]
    [InlineData("/dm")]
    [InlineData("/player")]
    [InlineData("/editor")]
    public async Task Every_stylesheet_and_script_a_page_links_is_served(string page)
    {
        string html = await fixture.Client.GetStringAsync(page, CancellationToken.None);
        List<string> assets = Linked(html);

        var broken = new List<string>();
        foreach (string asset in assets)
        {
            using HttpResponseMessage response = await fixture.Client.GetAsync("/" + asset, CancellationToken.None);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                broken.Add($"{asset} answers {(int)response.StatusCode}");
            }
        }

        Assert.Contains("app.css", assets);
        Assert.Contains("_framework/blazor.web.js", assets);
        Assert.Empty(broken);
    }

    // The href of every stylesheet and the src of every script, relative to the page's <base href="/">.
    private static List<string> Linked(string html) =>
        LinkPattern().Matches(html).Select(match => match.Groups["url"].Value).ToList();

    [GeneratedRegex("""<(?:link rel="stylesheet" href|script src)="(?<url>[^"]+)""")]
    private static partial Regex LinkPattern();
}
