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
/// Renders the Room tab's reading of a room to HTML: the order it is read in, what reaches its
/// read-aloud box (the DM reads that box out to the players, so only the room's authored read-aloud
/// may land in it), and how the area's creatures and exits are shown.
/// </summary>
public sealed class RoomReadingTests
{
    private const string RoomId = "data_dossiers_level_1_area_1";

    [Fact]
    public async Task The_room_reads_in_the_order_it_is_read_at_the_table()
    {
        // The read-aloud is what the DM says first; it used to sit under the encounter, the note and
        // nine sub-tabs, below the fold on the table's laptop.
        var dossier = new AreaDossier
        {
            AreaNodeId = RoomId,
            ReadAloud = "Smoke hangs under the rafters.",
            SkillChecks = new[] { new SkillCheck { Ability = "Wisdom (Perception)", Dc = 12 } },
            Glance = new[] { new DossierBlock { Heading = "Fire", Body = "Broken pews burn." } },
            Detail = new[] { new DossierBlock { Heading = "The loot", Body = "Under the altar." } },
        };
        var briefing = new RoomBriefing
        {
            RoomId = RoomId,
            Occupants = new[] { new OccupantEntry { Label = "Goblin", NodeId = "goblin", Count = "3" } },
            Connections = new[] { new ConnectionEntry { TargetRoomId = "area-2", Label = "Nave" } },
        };

        string html = await Render(new Dictionary<string, object>
        {
            [nameof(RoomReading.Briefing)] = briefing,
            [nameof(RoomReading.Dossier)] = dossier,
            [nameof(RoomReading.Encounter)] = (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>ENCOUNTER-SLOT</p>")),
            [nameof(RoomReading.Note)] = (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>NOTE-SLOT</p>")),
        });

        int[] order =
        {
            html.IndexOf("Smoke hangs under the rafters.", StringComparison.Ordinal),
            html.IndexOf("Perception DC 12", StringComparison.Ordinal),
            html.IndexOf("At a glance", StringComparison.Ordinal),
            html.IndexOf("ENCOUNTER-SLOT", StringComparison.Ordinal),
            html.IndexOf("The loot", StringComparison.Ordinal),
            html.IndexOf("NOTE-SLOT", StringComparison.Ordinal),
            html.IndexOf("Occupants (1)", StringComparison.Ordinal),
            html.IndexOf("Connections (1)", StringComparison.Ordinal),
        };

        Assert.DoesNotContain(-1, order);
        Assert.Equal(order.OrderBy(index => index), order);
    }

    [Fact]
    public async Task Who_is_here_is_folded_and_the_ways_out_stay_open()
    {
        // Who is here repeats the encounter, so it folds. The connections stay open until the Room
        // tab lists the neighbouring rooms itself.
        string html = await Render(new RoomBriefing { RoomId = RoomId }, new AreaDossier());

        Assert.Contains("<details class=\"room-more room-occupants\"><summary>Occupants (0)</summary>", html, StringComparison.Ordinal);
        Assert.Contains("<details class=\"room-more room-connections\" open><summary>Connections (0)</summary>", html, StringComparison.Ordinal);
    }

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
    public async Task The_room_lists_the_occupants_and_the_connections_and_nothing_else()
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

    private static Task<string> Render(RoomBriefing briefing, AreaDossier dossier) =>
        Render(new Dictionary<string, object>
        {
            [nameof(RoomReading.Briefing)] = briefing,
            [nameof(RoomReading.Dossier)] = dossier,
        });

    private static async Task<string> Render(Dictionary<string, object> parameters)
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<RoomReading>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
