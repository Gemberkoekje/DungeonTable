using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Tests.Maps;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The real app on a two-map copy of the sample pack: the DM has gone down to the second map, and the
/// projector still shows the first, framed and revealed as it was, until the DM shows the players the new
/// one. (The dashed Player view box is drawn by script, and was checked in a browser.)
/// </summary>
public sealed class ProjectorHoldHostTests : IDisposable
{
    private const string LowerMap = "level-2-crypt";
    private const string Landing = "r-30e1bf06";
    private const string NotYetParted = "The mists have not yet parted";

    private static readonly MapBounds UpperFraming = new MapBounds(1200, 300, 1600, 525);
    private static readonly MapBounds LowerFraming = new MapBounds(0, 0, 800, 450);

    private readonly string maps = Path.Combine(Path.GetTempPath(), "dt-hold-" + Guid.NewGuid().ToString("N"));

    public ProjectorHoldHostTests()
    {
        // The pack's map, and the same drawing again as a second floor with no rooms marked on it.
        string adventure = Path.Combine(maps, SamplePack.Adventure);
        Directory.CreateDirectory(adventure);
        foreach (string file in Directory.GetFiles(Path.Combine(SamplePack.Maps, SamplePack.Adventure)))
        {
            File.Copy(file, Path.Combine(adventure, Path.GetFileName(file)));
        }

        File.Copy(SamplePack.MapFile, Path.Combine(adventure, LowerMap + ".ds"));
        File.WriteAllText(
            Path.Combine(adventure, LowerMap + ".regions.json"),
            "{ \"mapId\": \"" + LowerMap + "\", \"adventure\": \"demo\", \"levelName\": \"Level 2 - The Crypt\" }");
    }

    [Fact]
    public async Task The_projector_shows_the_floor_the_players_are_on_until_the_dm_shows_them_the_next()
    {
        using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = Client(factory);
        SessionState session = factory.Services.GetRequiredService<SessionState>();
        DownAFloor(session);

        // The players: the first floor, framed where it was. Before, the projector followed the DM down
        // and showed the second floor as soon as the DM opened it.
        string player = await client.GetStringAsync("/player", CancellationToken.None);
        Assert.DoesNotContain(NotYetParted, player, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"1200 300 400 225\"", player, StringComparison.Ordinal);
        Assert.DoesNotContain("viewBox=\"0 0 800 450\"", player, StringComparison.Ordinal);

        // The DM: on the second floor, told where the projector is and offered to move it, and told what
        // the players see there (the Landing), not on the floor the DM is on (nothing yet).
        string dm = await client.GetStringAsync("/dm", CancellationToken.None);
        string held = Between(dm, "class=\"projector-held\"", "</button>");
        Assert.Contains("Projector on Level 1 - The Undercroft", held, StringComparison.Ordinal);
        Assert.Contains(">Show on projector", held, StringComparison.Ordinal);
        Assert.Contains("Players see 1 region", dm, StringComparison.Ordinal);

        session.ShowCurrentMapOnProjector();

        player = await client.GetStringAsync("/player", CancellationToken.None);
        Assert.Contains("viewBox=\"0 0 800 450\"", player, StringComparison.Ordinal);
        dm = await client.GetStringAsync("/dm", CancellationToken.None);
        Assert.DoesNotContain("class=\"projector-held\"", dm, StringComparison.Ordinal);
        Assert.Contains("Players see 0 regions", dm, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(maps))
        {
            Directory.Delete(maps, recursive: true);
        }
    }

    // The party is on the first floor with the Landing showing, and the DM has gone down to the second
    // and framed it, as opening it on the DM screen does.
    private static void DownAFloor(SessionState session)
    {
        session.SetCurrentMap(SamplePack.MapId);
        session.MarkSeeded(SamplePack.MapId);
        session.ToggleRegion(Landing);
        session.SetPlayerViewport(UpperFraming);

        session.SetCurrentMap(LowerMap);
        session.MarkSeeded(LowerMap);
        session.SetPlayerViewport(LowerFraming);
    }

    private WebApplicationFactory<Program> Factory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            SamplePack.Serve(builder);
            builder.UseSetting("Maps:Root", maps);
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
