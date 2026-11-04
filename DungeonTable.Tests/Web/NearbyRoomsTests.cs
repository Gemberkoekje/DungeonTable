using System.Collections.Generic;
using System.Threading.Tasks;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Web.Components.Dm;
using DungeonTable.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonTable.Tests.Web;

/// <summary>
/// The Room tab's Nearby list and the peek card it shares with the map: one line per way out, with the
/// badges and the creatures of the room it leads to, and the card with what is in that room.
/// </summary>
public sealed class NearbyRoomsTests
{
    private static readonly RoomGlance Workshop = new RoomGlance
    {
        AreaNodeId = "area-workshop",
        Title = "Bell-Founder's Workshop",
        AreaNumber = 4,
        Creatures = new[]
        {
            new EncounterMonster { NodeId = "grask", Name = "Grask" },
            new EncounterMonster { NodeId = "bugbear", Name = "Bugbear", SuggestedCount = 2 },
            new EncounterMonster { NodeId = "tam-brask", Name = "Tam Brask", Secret = true },
        },
        Loot = new[] { "Bronze bell" },
        Checks = new[] { new SkillCheck { Ability = "Strength (Athletics)", Dc = 15 } },
        Hazards = new[] { "Hazard: the furnace" },
        Secrets = new[] { "Secret way to Vestry" },
        Exits = new[]
        {
            new ConnectionEntry { TargetRoomId = "area-den", Label = "Goblin Den" },
            new ConnectionEntry { TargetRoomId = "area-vestry", Label = "Vestry", Secret = true },
        },
    };

    [Fact]
    public async Task Each_way_out_shows_the_badges_and_who_is_still_up_in_the_room_it_leads_to()
    {
        RoomBadges badges = Badges(stillUp: 2, note: string.Empty);

        string html = await RenderNearby(new NearbyRoom(new ConnectionEntry { TargetRoomId = "area-workshop", Label = "Bell-Founder's Workshop", Note = "door east" }, Workshop, badges));

        Assert.Contains("<h3>Nearby</h3>", html, StringComparison.Ordinal);
        Assert.Contains(
            "<span class=\"label\">Bell-Founder&#x27;s Workshop</span><span class=\"room-badges\"><span class=\"room-badge badge-creatures\" title=\"2 creatures not yet ticked off\">&#x2620;&#xFE0E; 2</span>",
            html,
            StringComparison.Ordinal);
        Assert.Contains("<span class=\"nearby-who\">Grask, Tam Brask</span><span class=\"briefing-note\">door east</span>", html, StringComparison.Ordinal);
        Assert.Contains("<div class=\"nearby-peek\"><div class=\"room-peek\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_secret_way_is_marked_one_to_an_unwritten_floor_waits_and_a_broken_one_shows()
    {
        string html = await RenderNearby(
            new NearbyRoom(new ConnectionEntry { TargetRoomId = "area-vestry", Label = "Vestry", Secret = true }, RoomGlance.None, RoomBadges.None),
            new NearbyRoom(new ConnectionEntry { Label = "L2 area 1", Pending = true, Note = "the stairs down" }, RoomGlance.None, RoomBadges.None),
            new NearbyRoom(new ConnectionEntry { Label = "area 99" }, RoomGlance.None, RoomBadges.None));

        Assert.Contains("<li class=\"nearby-row secret\"><div class=\"nearby-line\"><span class=\"nearby-arrow\" aria-hidden=\"true\">&rarr;</span><span class=\"label\">Vestry</span><span class=\"secret-badge\" title=\"DM only — the players don't know this way exists\">DM</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"xref-pending\" title=\"Not authored yet: L2 area 1 is on a floor with no dossier\">L2 area 1</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"briefing-note\">the stairs down</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"xref-unresolved\" title=\"Broken: area 99 names no authored area\">area 99</span>", html, StringComparison.Ordinal);

        // A room with nothing written has no card to show.
        Assert.DoesNotContain("nearby-peek", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_room_with_no_ways_out_says_so()
    {
        string html = await RenderNearby();

        Assert.Contains("No way out is written for this room.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_peek_card_says_what_is_in_the_room_and_strikes_through_who_is_ticked_off()
    {
        string html = await Render<RoomPeek>(new Dictionary<string, object>
        {
            [nameof(RoomPeek.Glance)] = Workshop,
            [nameof(RoomPeek.Badges)] = Badges(stillUp: 2, note: "Tam asked us to free him.\nThen: the bell."),
        });

        Assert.Contains("<span class=\"peek-number\">Area 4</span><strong class=\"peek-title\">Bell-Founder&#x27;s Workshop</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<span>Grask</span>, <span class=\"peek-done\" title=\"Ticked off\">2 &#xD7; Bugbear</span>, <span class=\"secret\" title=\"DM only: the players don&#x27;t know this is here\">Tam Brask</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"peek-key\">Loot</span><span class=\"peek-text\">Bronze bell</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"peek-key\">Checks</span><span class=\"peek-text\">Athletics DC 15</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"peek-key\">Danger</span><span class=\"peek-text\">Hazard: the furnace</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"peek-key\">Secret</span><span class=\"peek-text\">Secret way to Vestry</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span>Goblin Den</span> · <span class=\"secret\">Vestry (secret)</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"peek-key\">Your note</span><span class=\"peek-text\">Tam asked us to free him.</span>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_room_that_lists_nobody_says_so_on_its_card()
    {
        var empty = new RoomGlance { AreaNodeId = "area-landing", Title = "Landing" };

        string html = await Render<RoomPeek>(new Dictionary<string, object>
        {
            [nameof(RoomPeek.Glance)] = empty,
            [nameof(RoomPeek.Badges)] = RoomBadges.None,
        });

        Assert.Contains("nobody listed", html, StringComparison.Ordinal);
        Assert.DoesNotContain("peek-loot", html, StringComparison.Ordinal);
        Assert.DoesNotContain("peek-note", html, StringComparison.Ordinal);
    }

    // The workshop's badges with its first creature and its third still up and the bugbears ticked off.
    private static RoomBadges Badges(int stillUp, string note) => new RoomBadges
    {
        StillUp = new[] { Workshop.Creatures[0], Workshop.Creatures[2] },
        Creatures = stillUp.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Loot = true,
        Hazard = true,
        Secret = true,
        Note = note,
    };

    private static Task<string> RenderNearby(params NearbyRoom[] rooms) =>
        Render<NearbyRooms>(new Dictionary<string, object> { [nameof(NearbyRooms.Rooms)] = rooms });

    private static async Task<string> Render<TComponent>(Dictionary<string, object> parameters)
        where TComponent : IComponent
    {
        await using ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent output = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return output.ToHtmlString();
        });
    }
}
