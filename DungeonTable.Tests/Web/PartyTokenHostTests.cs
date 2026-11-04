using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Maps;
using DungeonTable.Infrastructure.Maps;
using DungeonTable.Tests.Maps;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The real app on the sample pack, with the party already in the Goblin Den: the DM screen opens on the
/// party's room and says the party is there, and the lifter is gone with the Party tool in its place.
/// (That the projector draws no token is checked in a browser: the token is drawn by script, on the DM's
/// canvas only.)
/// </summary>
public sealed class PartyTokenHostTests
{
    private const string GoblinDen = "data_dossiers_level_1_area_2";

    [Fact]
    public async Task The_dm_screen_opens_on_the_party_s_room()
    {
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));

        MapPoint den = await MiddleOfAreaAsync(GoblinDen);
        SessionState session = factory.Services.GetRequiredService<SessionState>();
        session.SetCurrentMap(SamplePack.MapId);
        session.MarkSeeded(SamplePack.MapId);
        session.MoveParty(den.X, den.Y, Array.Empty<(int, int)>(), joinLast: false);

        string dm = await client.GetStringAsync("/dm", CancellationToken.None);

        Assert.Contains("Goblin Den", Between(dm, "class=\"room-title\"", "</h2>"), StringComparison.Ordinal);
        Assert.Contains("class=\"room-chip party\"", dm, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"room-follow\"", dm, StringComparison.Ordinal);
        Assert.Contains("class=\"room-action pin ", dm, StringComparison.Ordinal);
        Assert.Contains("follow-btn", dm, StringComparison.Ordinal);
        Assert.Matches("<span class=\"tool-label\">Party</span>\\s*<span class=\"tool-key\" aria-hidden=\"true\">4</span>", dm);
        Assert.DoesNotContain("Lifter", dm, StringComparison.Ordinal);
    }

    private static async Task<MapPoint> MiddleOfAreaAsync(string area)
    {
        var store = new FileSystemMapStore(SamplePack.Maps);
        MapDefinition map = (await store.LoadAsync(SamplePack.MapId, CancellationToken.None)).Value;
        Region region = map.Regions.First(r => string.Equals(r.GraphNodeId, area, StringComparison.Ordinal));
        return new MapPoint(region.Polygon.Average(p => p.X), region.Polygon.Average(p => p.Y));
    }

    private static string Between(string html, string start, string end)
    {
        int from = html.IndexOf(start, StringComparison.Ordinal);
        Assert.True(from >= 0, $"No {start} on the page.");
        int to = html.IndexOf(end, from, StringComparison.Ordinal);
        return html[from..to];
    }
}
