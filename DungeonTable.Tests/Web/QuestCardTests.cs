using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Dossier;
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
/// A quest beat's destination chip: a jump to an area that is written, and otherwise a label, marked
/// "not authored yet" only while its floor is unwritten. A beat on a floor, or at a place reached
/// through one, used to carry that mark even on the floor the campaign opens on.
/// </summary>
public sealed class QuestCardTests
{
    private const string Workshop = "data_dossiers_level_1_area_4";

    private static readonly Quest Quest = new Quest
    {
        Id = "the-silent-bell",
        Title = "The Silent Bell",
        Beats = new[]
        {
            Beat("down", new QuestTarget { Kind = QuestTargetKind.Level, Level = 1, PlaceLabel = "The Undercroft" }),
            Beat("workshop", new QuestTarget { Kind = QuestTargetKind.Area, Level = 1, AreaKey = "4", PlaceLabel = "Bell-Founder's Workshop", NodeId = Workshop }),
            Beat("crypt", new QuestTarget { Kind = QuestTargetKind.Area, Level = 2, AreaKey = "1", PlaceLabel = "The Sealed Crypt", NodeId = "data_dossiers_level_2_area_1" }),
            Beat("ossuary", new QuestTarget { Kind = QuestTargetKind.Place, Level = 2, PlaceLabel = "The Founders' Ossuary" }),
            Beat("belfry", new QuestTarget { Kind = QuestTargetKind.Place, Level = 1, PlaceLabel = "The Belfry Stair" }),
            Beat("mill", new QuestTarget { Kind = QuestTargetKind.Surface, PlaceLabel = "The mill" }),
        },
    };

    [Fact]
    public async Task A_beat_on_a_written_floor_is_not_marked_as_waiting_for_it()
    {
        string html = await Render();

        Assert.Contains("title=\"On level 1, which is written in the app\">Level 1 &#x2014; The Undercroft</span>", html, StringComparison.Ordinal);
        Assert.Contains(">The Belfry Stair (via level 1)</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_beat_on_an_unwritten_floor_still_waits_for_it()
    {
        string html = await Render();

        Assert.Contains(">The Founders&#x27; Ossuary (via level 2) &#xB7; not authored yet</span>", html, StringComparison.Ordinal);
        Assert.Contains(">Level 2 &#xB7; area 1 &#x2014; The Sealed Crypt &#xB7; not authored yet</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_written_area_is_a_jump_and_the_village_is_a_plain_label()
    {
        string html = await Render();

        Assert.Contains("title=\"Jump to this area&#x27;s briefing\">Level 1 &#xB7; area 4 &#x2014; Bell-Founder&#x27;s Workshop</button>", html, StringComparison.Ordinal);
        Assert.Contains("title=\"Above ground &#x2014; not a dungeon area\">The mill</span>", html, StringComparison.Ordinal);
    }

    private static QuestBeat Beat(string id, QuestTarget target) => new QuestBeat { Id = id, Summary = id, Target = target };

    private static async Task<string> Render()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        await using ServiceProvider services = new ServiceCollection().AddSingleton(campaign).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object>
            {
                [nameof(QuestCard.Quest)] = Quest,
                [nameof(QuestCard.StartOpen)] = true,
                [nameof(QuestCard.IsAuthored)] = (Func<string, bool>)(id => id == Workshop),
                [nameof(QuestCard.IsFloorAuthored)] = (Func<int, bool>)(level => level == 1),
                [nameof(QuestCard.OnNavigate)] = EventCallback.Factory.Create<string>(new object(), (string _) => { }),
            });

            HtmlRootComponent output = await renderer.RenderComponentAsync<QuestCard>(parameters);
            return output.ToHtmlString();
        });
    }
}
