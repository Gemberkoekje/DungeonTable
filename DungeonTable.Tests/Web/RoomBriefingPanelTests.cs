using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Components.Dm;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Renders the briefing panel's Room tab to HTML: what reaches its read-aloud box (the DM reads that
/// box out to the players, so only the room's authored read-aloud may land in it), and how the area's
/// creatures and exits are shown.
/// </summary>
public sealed class RoomBriefingPanelTests
{
    private const string RoomId = "data_dossiers_level_1_area_1";

    [Fact]
    public async Task A_room_with_no_read_aloud_shows_no_read_aloud_box()
    {
        string html = await Render(new RoomBriefing { RoomId = RoomId, Label = "Test Room" }, new AreaDossier { AreaNodeId = RoomId });

        Assert.DoesNotContain("read-aloud", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_read_aloud_box_holds_the_authored_read_aloud_and_nothing_else()
    {
        var dossier = new AreaDossier { AreaNodeId = RoomId, ReadAloud = "Dust hangs in the still air." };

        string html = await Render(new RoomBriefing { RoomId = RoomId, Label = "Test Room" }, dossier);

        Assert.Contains(
            "<blockquote class=\"read-aloud\">Dust hangs in the still air.</blockquote>", html, StringComparison.Ordinal);
        Assert.Equal(1, html.Split("class=\"read-aloud\"").Length - 1);
    }

    [Fact]
    public async Task The_room_tab_lists_the_occupants_and_the_connections_and_nothing_else()
    {
        // The tab once listed a room's features, items and contents as well; the room's own prose
        // carries all of it.
        string html = await Render(new RoomBriefing { RoomId = RoomId }, new AreaDossier());

        Assert.Contains("Occupants (0)", html, StringComparison.Ordinal);
        Assert.Contains("Connections (0)", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Features (", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Items (", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Contents (", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Occupants_show_their_counts_notes_and_secrets_and_a_broken_one_is_marked()
    {
        var briefing = new RoomBriefing
        {
            RoomId = RoomId,
            Occupants = new[]
            {
                new OccupantEntry { NodeId = "bandit-node", MonsterRef = "bandit-node", Label = "Bandit", Count = "4", Note = "playing cards" },
                new OccupantEntry { NodeId = "golem", MonsterRef = "golem-node", Label = "Grask's guard", Secret = true },
                new OccupantEntry { Label = "bandit-captin" },
            },
        };

        string html = await Render(briefing, new AreaDossier());

        Assert.Contains(
            "<li><span class=\"occupant-count\">4 &#xD7;</span><span class=\"label\">Bandit</span><span class=\"briefing-note\">playing cards</span></li>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<li class=\"secret\"><span class=\"label\">Grask&#x27;s guard</span><span class=\"secret-badge\" title=\"DM only — the players don't know this is here\">DM</span></li>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<span class=\"xref-unresolved\" title=\"Broken: bandit-captin names no stat block, entity or person that is loaded\">bandit-captin</span>",
            html,
            StringComparison.Ordinal);

        // An individual opens its own card, which shows the block: no raw id in the chip.
        Assert.DoesNotContain("golem-node", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Exits_to_an_unwritten_floor_wait_and_a_broken_one_is_marked()
    {
        var briefing = new RoomBriefing
        {
            RoomId = RoomId,
            Connections = new[]
            {
                new ConnectionEntry { TargetRoomId = "area-2", Label = "Nave", Note = "through the arch" },
                new ConnectionEntry { Label = "L2 area 1", Pending = true, Note = "the stairs down" },
                new ConnectionEntry { Label = "area 99", Secret = true },
            },
        };

        string html = await Render(briefing, new AreaDossier());

        Assert.Contains(
            "<li><span class=\"label\">Nave</span><span class=\"briefing-note\">through the arch</span></li>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<li><span class=\"xref-pending\" title=\"Not authored yet: L2 area 1 is on a floor with no dossier\">L2 area 1</span><span class=\"briefing-note\">the stairs down</span></li>",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "<li class=\"secret\"><span class=\"xref-unresolved\" title=\"Broken: area 99 names no authored area\">area 99</span><span class=\"secret-badge\" title=\"DM only — the players don't know this way exists\">DM</span></li>",
            html,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Level")]
    [InlineData("Rules")]
    public async Task Campaign_and_rules_prose_is_read_on_no_floor(string tab)
    {
        // campaign.json and reference.json belong to no floor. The content tests and the format read
        // them on none, so a bare [[area 3]] there is broken; the DM screen read them on the floor on
        // show, where the same link worked on one floor and pointed elsewhere on the next.
        const string Prose = "Take the ringers' stair down.";
        var onTheShownFloor = new ScriptedCrossRefResolver().Add("the ringers' stair", "data_dossiers_level_1_area_5", CrossRefKind.Area);
        var onNoFloor = new ScriptedCrossRefResolver();
        var block = new DossierBlock { Heading = "The way down", Body = Prose };

        string html = await Render(new Dictionary<string, object>
        {
            [nameof(RoomBriefingPanel.InitialTab)] = tab,
            [nameof(RoomBriefingPanel.Campaign)] = new CampaignDossier { Title = "The Silent Bell", Sections = new[] { block } },
            [nameof(RoomBriefingPanel.Reference)] = new ReferenceLibrary { Sections = new[] { block } },
            [nameof(RoomBriefingPanel.ResolveRefs)] = (Func<string, IReadOnlyList<CrossRef>>)(text => onTheShownFloor.Resolve(text, "data_dossiers_level_1")),
            [nameof(RoomBriefingPanel.ResolveEntityRefs)] = (Func<string, IReadOnlyList<CrossRef>>)onNoFloor.Resolve,
        });

        Assert.Contains("<div class=\"detail-body\">Take the ringers&#x27; stair down.</div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("xref-area", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_rules_tab_is_headed_in_the_campaigns_own_words_or_not_at_all()
    {
        // It said "Dungeon features" over every campaign's rules: one book's wording, left in the app.
        var sections = new[] { new DossierBlock { Heading = "Darkness Below", Body = "Count the lanterns." } };

        string titled = await Render(new Dictionary<string, object>
        {
            [nameof(RoomBriefingPanel.InitialTab)] = "Rules",
            [nameof(RoomBriefingPanel.Reference)] = new ReferenceLibrary { Title = "Rules of the undercroft", Sections = sections },
        });
        string untitled = await Render(new Dictionary<string, object>
        {
            [nameof(RoomBriefingPanel.InitialTab)] = "Rules",
            [nameof(RoomBriefingPanel.Reference)] = new ReferenceLibrary { Sections = sections },
        });

        Assert.Contains("<div class=\"detail\"><h3>Rules of the undercroft</h3><details", titled, StringComparison.Ordinal);
        Assert.Contains("<div class=\"detail\"><details", untitled, StringComparison.Ordinal);
        Assert.DoesNotContain("Dungeon features", titled + untitled, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<string> Render(RoomBriefing briefing, AreaDossier dossier) =>
        Render(new Dictionary<string, object>
        {
            [nameof(RoomBriefingPanel.Briefing)] = briefing,
            [nameof(RoomBriefingPanel.Dossier)] = dossier,
        });

    private static async Task<string> Render(Dictionary<string, object> parameters)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<RoomBriefingPanel>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
