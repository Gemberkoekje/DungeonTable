using System.Threading;
using System.Threading.Tasks;
using DungeonTable.Tests.Maps;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The projector glides from one view to the next (dt-glide.js): the script slides a wrapper around the
/// map and the DM's pointer, and glides only while the wrapper names the same map. The page has to draw
/// them inside it, or the players see the map jump as before.
/// </summary>
public sealed class ProjectorGlideHostTests : IClassFixture<MapServingFixture>
{
    private readonly MapServingFixture fixture;

    public ProjectorGlideHostTests(MapServingFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task The_projector_draws_its_map_inside_the_wrapper_the_glide_moves()
    {
        string html = await fixture.Client.GetStringAsync("/player", CancellationToken.None);

        int wrapper = html.IndexOf("<div class=\"player-glide\" data-map=\"trap-map\">", StringComparison.Ordinal);
        int map = html.IndexOf("<svg xmlns=\"http://www.w3.org/2000/svg\"", StringComparison.Ordinal);
        int turn = html.IndexOf("<div class=\"player-turn", StringComparison.Ordinal);
        Assert.True(wrapper > turn && turn >= 0, "the wrapper sits inside the turned box");
        Assert.True(map > wrapper, "the map is drawn inside the wrapper");
    }
}
