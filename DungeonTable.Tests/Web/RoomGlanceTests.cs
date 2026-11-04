using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;
using DungeonTable.Core.Maps.Vector;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// What a room shows at a glance, from what the content and the map already hold: the marks the Room
/// Editor put in it, its loot and secrets, and the badges those make with the table's checklist and notes.
/// </summary>
public sealed class RoomGlanceTests
{
    private const string Den = "area-den";
    private const double Cell = 10;

    // The den: a 10 by 6 square room from (0, 0) to (100, 60).
    private static readonly Region[] DenRegions = { Rect("r-den", Den, 0, 0, 100, 60) };

    private static readonly string[] TrapAndTwoHazards = { "Trap: pit", "Hazard", "Hazard" };
    private static readonly string[] TwoSecretDoors = { "2 secret doors" };
    private static readonly string[] DoorAndMarkers = { "Secret door", "Secret", "Hidden door: behind the altar" };
    private static readonly string[] DenLoot = { "Hidden purse", "Stolen candlesticks" };
    private static readonly string[] DenSecrets = { "Secret door", "Secret way to Workshop" };
    private static readonly string[] Bugbears = { "bugbear" };
    private static readonly BadgeKind[] JustCleared = { BadgeKind.Cleared };
    private static readonly string[] EveryBadge = { "☠\uFE0E 1d4+3", "◆", "⚠\uFE0E", "S", "✎\uFE0E" };
    private static readonly string[] MainRoom = { "r-main" };
    private static readonly string[] MainAndStub = { "r-main", "r-stub" };
    private static readonly string[] Trap = { "Trap" };
    private static readonly string[] SecretDoor = { "Secret door" };
    private static readonly MapBounds[] NoDoors = Array.Empty<MapBounds>();

    [Fact]
    public void A_trap_or_hazard_counts_where_it_stands_or_where_it_is_linked()
    {
        FeatureMarker[] markers =
        {
            Marker("in", FeatureKind.Trap, 50, 30, label: "pit"),
            Marker("linked", FeatureKind.Hazard, 500, 500, area: Den),
            Marker("elsewhere", FeatureKind.Trap, 300, 30),
            Marker("other-room", FeatureKind.Hazard, 50, 30, area: "area-nave"),
        };

        RoomMarks marks = RoomMarks.Within(Den, DenRegions, markers, NoDoors, Cell);

        // A marker linked to another room but standing in this one is physically here, so it counts too.
        Assert.Equal(TrapAndTwoHazards, marks.Hazards);
        Assert.Empty(marks.Secrets);
    }

    [Fact]
    public void A_secret_door_counts_in_the_walls_of_the_room_and_not_further_off()
    {
        MapBounds[] doors =
        {
            new MapBounds(98, 25, 104, 35),   // on the east wall, its middle a square outside the region's edge at most
            new MapBounds(40, -6, 60, -2),    // on the north wall, just outside
            new MapBounds(130, 25, 136, 35),  // three squares off: another room's
        };

        RoomMarks marks = RoomMarks.Within(Den, DenRegions, Array.Empty<FeatureMarker>(), doors, Cell);

        Assert.Equal(TwoSecretDoors, marks.Secrets);
    }

    [Fact]
    public void Secret_and_hidden_door_markers_are_secrets_and_a_room_with_no_marks_has_none()
    {
        FeatureMarker[] markers =
        {
            Marker("s", FeatureKind.Secret, 20, 20),
            Marker("h", FeatureKind.HiddenDoor, 80, 20, label: "behind the altar"),
        };
        MapBounds[] door = { new MapBounds(48, 28, 52, 32) };

        RoomMarks marks = RoomMarks.Within(Den, DenRegions, markers, door, Cell);

        Assert.Equal(DoorAndMarkers, marks.Secrets);
        Assert.Same(RoomMarks.None, RoomMarks.Within(Den, DenRegions, Array.Empty<FeatureMarker>(), NoDoors, Cell));
    }

    [Fact]
    public void A_glance_holds_the_loot_the_secret_ways_and_the_ways_out_of_the_room()
    {
        var dossier = new AreaDossier
        {
            AreaNodeId = Den,
            Title = "Goblin Den",
            AreaNumber = 2,
            Glance = new[]
            {
                new DossierBlock { Heading = "Fire", Kind = DossierBlockKind.Feature },
                new DossierBlock { Heading = "Hidden purse", Kind = DossierBlockKind.Loot, Secret = true },
            },
            Detail = new[]
            {
                new DossierBlock { Heading = "Stolen candlesticks", Kind = DossierBlockKind.Loot },
                new DossierBlock { Heading = "The loose flagstone", Kind = DossierBlockKind.Secret },
            },
            SkillChecks = new[] { new SkillCheck { Ability = "Wisdom (Perception)", Dc = 12 } },
        };
        var briefing = new RoomBriefing
        {
            Connections = new[]
            {
                new ConnectionEntry { TargetRoomId = "area-landing", Label = "Landing" },
                new ConnectionEntry { TargetRoomId = "area-workshop", Label = "Workshop", Secret = true },
            },
        };
        var marks = new RoomMarks { Hazards = Trap, Secrets = SecretDoor };

        RoomGlance glance = RoomGlance.From(dossier, briefing, new[] { Monster("goblin", 3) }, marks);

        // The blocks marked secret (the hidden purse, the loose flagstone) are DM-only prose, which nearly
        // every room on a real floor has: they are not secret ways.

        Assert.Equal("Goblin Den", glance.Title);
        Assert.Equal(2, glance.AreaNumber);
        Assert.Equal(DenLoot, glance.Loot);
        Assert.Equal("Trap", Assert.Single(glance.Hazards));
        Assert.Equal(DenSecrets, glance.Secrets);
        Assert.Equal(2, glance.Exits.Count);
        Assert.Single(glance.Checks);
        Assert.True(glance.HasSecret);
    }

    [Fact]
    public void Only_a_secret_way_is_a_secret_and_an_unwritten_room_has_no_glance()
    {
        // A creature the players must not know of shows in the creature badge, and a DM-only block is
        // DM-only prose; neither is a hidden way, so neither makes the room's S.
        var dossier = new AreaDossier
        {
            AreaNodeId = Den,
            Title = "Nave",
            Detail = new[] { new DossierBlock { Heading = "The Bell-Warden Wakes", Kind = DossierBlockKind.Secret, Secret = true } },
        };
        var hidden = new EncounterMonster { NodeId = "bell-warden", Name = "The Bell-Warden", Secret = true };
        var secretWay = new RoomBriefing { Connections = new[] { new ConnectionEntry { TargetRoomId = "area-vestry", Label = "Vestry", Secret = true } } };

        Assert.False(RoomGlance.From(dossier, new RoomBriefing(), new[] { hidden }, RoomMarks.None).HasSecret);
        Assert.True(RoomGlance.From(dossier, secretWay, new[] { hidden }, RoomMarks.None).HasSecret);
        Assert.True(RoomGlance.From(dossier, new RoomBriefing(), Array.Empty<EncounterMonster>(), new RoomMarks { Secrets = SecretDoor }).HasSecret);
        Assert.Same(RoomGlance.None, RoomGlance.From(new AreaDossier(), new RoomBriefing(), Array.Empty<EncounterMonster>(), RoomMarks.None));
    }

    [Fact]
    public void The_creature_badge_counts_what_is_not_ticked_off_and_cleared_takes_its_place()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        RoomGlance glance = Glance(Monster("grask", 1), Monster("bugbear", 2));

        Assert.Equal("3", CountOf(glance, campaign));

        campaign.ToggleCreatureCleared(Den, "grask");
        RoomBadges partly = RoomBadges.For(glance, campaign);
        Assert.Equal("2", partly.Creatures);
        Assert.False(partly.Cleared);
        Assert.Equal(Bugbears, partly.StillUp.Select(row => row.NodeId));

        campaign.ToggleCreatureCleared(Den, "bugbear");
        RoomBadges done = RoomBadges.For(glance, campaign);
        Assert.Equal(string.Empty, done.Creatures);
        Assert.True(done.Cleared);
        Assert.Equal(JustCleared, done.Parts.Select(part => part.Kind));
    }

    [Fact]
    public void A_room_that_lists_nobody_is_never_cleared()
    {
        RoomBadges badges = RoomBadges.For(Glance(), new CampaignState(new FakePartyRoster()));

        Assert.False(badges.Cleared);
        Assert.True(badges.IsEmpty);
    }

    [Fact]
    public void The_badges_come_in_order_with_their_glyphs()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        campaign.SetNote(Den, "  Nib owes the party a favour.  ");
        var glance = new RoomGlance
        {
            AreaNodeId = Den,
            Creatures = new[] { new EncounterMonster { NodeId = "goblin", Name = "Goblin", CountExpression = "1d4+2" }, Monster("nib", 1) },
            Loot = DenLoot,
            Hazards = Trap,
            Secrets = SecretDoor,
        };

        RoomBadges badges = RoomBadges.For(glance, campaign);

        Assert.Equal(EveryBadge, badges.Parts.Select(part => part.Text));
        Assert.Equal("1d4+3 creatures not yet ticked off", badges.Parts[0].Title);
        Assert.Equal("Nib owes the party a favour.", badges.Note);
    }

    [Fact]
    public void A_room_split_into_regions_shows_its_badges_once_under_the_largest_and_greys_them_all_when_cleared()
    {
        var campaign = new CampaignState(new FakePartyRoster());
        Region[] regions =
        {
            Rect("r-stub", Den, 100, 20, 120, 40),
            Rect("r-main", Den, 0, 0, 100, 60),
            Rect("r-nave", "area-nave", 200, 0, 260, 60),
            Rect("r-unlinked", string.Empty, 300, 0, 360, 60),
        };
        RoomGlance den = Glance(Monster("rat", 2));
        RoomGlance Lookup(string node)
        {
            if (string.Equals(node, Den, StringComparison.Ordinal))
            {
                return den;
            }

            return node.Length > 0 ? new RoomGlance { AreaNodeId = node } : RoomGlance.None;
        }

        MapBadges before = MapBadges.For(regions, Lookup, campaign);

        // The nave has nothing to show, so no badges at all; the den's sit under its main room.
        Assert.Equal(MainRoom, before.ByRegion.Keys);
        Assert.Equal("2", before.ByRegion["r-main"].Creatures);
        Assert.Empty(before.ClearedRegionIds);

        campaign.ToggleCreatureCleared(Den, "rat");
        MapBadges after = MapBadges.For(regions, Lookup, campaign);

        Assert.True(after.ByRegion["r-main"].Cleared);
        Assert.Equal(MainAndStub, after.ClearedRegionIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    private static string CountOf(RoomGlance glance, CampaignState campaign) => RoomBadges.For(glance, campaign).Creatures;

    private static RoomGlance Glance(params EncounterMonster[] creatures) =>
        new RoomGlance { AreaNodeId = Den, Title = "Goblin Den", Creatures = creatures };

    private static EncounterMonster Monster(string id, int count) =>
        new EncounterMonster { NodeId = id, Name = id, SuggestedCount = count };

    private static FeatureMarker Marker(string id, FeatureKind kind, double x, double y, string label = "", string area = "") =>
        new FeatureMarker { FeatureId = id, Kind = kind, Position = new MapPoint(x, y), Label = label, GraphNodeId = area };

    private static Region Rect(string id, string node, double minX, double minY, double maxX, double maxY) =>
        new Region
        {
            RegionId = id,
            GraphNodeId = node,
            Label = id,
            Polygon = new[] { new MapPoint(minX, minY), new MapPoint(maxX, minY), new MapPoint(maxX, maxY), new MapPoint(minX, maxY) },
        };
}
