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
/// The real app on copies of the sample pack, each with one hand-written <c>null</c> of the kind that
/// used to answer the DM screen with an error: an area's title, an NPC's id or name, a map region, a
/// party member. Each one now reads as if it were left out, and the pages answer.
/// </summary>
public sealed class HandWrittenJsonHostTests
{
    [Theory]
    [InlineData("data/dossiers/level-1.json", "areas[0].title")]
    [InlineData("data/dossiers/level-1.json", "areas[0]")]
    [InlineData("data/dossiers/level-1.json", "level")]
    [InlineData("data/dossiers/npcs.json", "npcs[0].id")]
    [InlineData("data/dossiers/npcs.json", "npcs[0].name")]
    [InlineData("data/dossiers/npcs.json", "npcs[0].armourClass")]
    [InlineData("data/dossiers/quests.json", "quests[0].beats[0].target")]
    [InlineData("data/dossiers/campaign.json", "startAreaNodeId")]
    [InlineData("Maps/demo/level-1-undercroft.regions.json", "regions[0]")]
    [InlineData("Maps/demo/level-1-undercroft.regions.json", "regions[0].label")]
    [InlineData("data/party.json", "members[0]")]
    [InlineData("data/party.json", "members[0].name")]
    public async Task The_dm_screen_and_the_player_view_answer(string document, string path)
    {
        using var pack = new PackCopy();
        pack.SetNull(document, path);
        using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            pack.Serve(builder);
            builder.UseSetting("Auth:Passphrase", MapServingFixture.Passphrase);
        });
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($":{MapServingFixture.Passphrase}")));

        HttpResponseMessage dm = await client.GetAsync("/dm", CancellationToken.None);
        HttpResponseMessage player = await client.GetAsync("/player", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, dm.StatusCode);
        Assert.Equal(HttpStatusCode.OK, player.StatusCode);
    }
}
