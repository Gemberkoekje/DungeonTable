using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Stats;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Components.Dm;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The encounter strip with nothing in it says why: the area lists nobody, so nobody is there.
/// </summary>
public sealed class EncounterStripRenderingTests
{
    [Fact]
    public async Task An_area_that_lists_nobody_says_so()
    {
        string html = await Render();

        Assert.Contains("Nobody is listed in this area.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("named in this area", html, StringComparison.Ordinal);
    }

    private static async Task<string> Render()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new BattleState(new FakePartyRoster(), new FakeStatLibrary()));
        services.AddSingleton(new CampaignState(new FakePartyRoster()));
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object>
        {
            [nameof(EncounterStrip.Monsters)] = Array.Empty<EncounterMonster>(),
            [nameof(EncounterStrip.AreaNodeId)] = "data_dossiers_level_1_area_24",
        };

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<EncounterStrip>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
