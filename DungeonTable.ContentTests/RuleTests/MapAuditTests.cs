using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Maps;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>
/// Proves the map rules catch annotations filed where a save would not write them, a map that cannot
/// be opened, a region that cannot be clicked or opens nothing, an area nobody can click, and a region
/// covering another.
/// </summary>
public sealed class MapAuditTests
{
    private const string Landing = "data_dossiers_level_1_area_1";
    private const string Nave = "data_dossiers_level_1_area_3a";

    private static readonly HashSet<int> FloorOneWritten = new() { 1 };

    private static readonly HashSet<string> Drawn = new(StringComparer.Ordinal) { "level-1-undercroft" };

    [Theory]
    [InlineData("demo/level-1-undercroft.regions.json", "level-1-undercroft", "demo")]
    [InlineData("level-1-undercroft.regions.json", "level-1-undercroft", "")]
    public void A_map_filed_under_its_own_id_and_adventure_is_not_reported(string path, string mapId, string adventure)
    {
        Assert.Empty(MapAudit.Misfiled(path, new MapDefinition { MapId = mapId, Adventure = adventure }));
    }

    [Theory]
    [InlineData("demo/level-1-undercroft.regions.json", "level-1", "demo", "Its mapId is 'level-1'")]
    [InlineData("demo/level-1-undercroft.regions.json", "level-1-undercroft", "crypt", "a save would write it to the 'crypt' folder")]
    [InlineData("level-1-undercroft.regions.json", "level-1-undercroft", "demo", "the file is in ''")]
    public void A_map_filed_where_a_save_would_not_write_it_is_reported(string path, string mapId, string adventure, string expected)
    {
        string fault = Assert.Single(MapAudit.Misfiled(path, new MapDefinition { MapId = mapId, Adventure = adventure }));

        Assert.Contains(expected, fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_map_that_shares_its_id_or_has_no_drawing_cannot_be_opened()
    {
        var map = new MapDefinition { MapId = "level-1-undercroft" };
        var copy = new MapDefinition { MapId = "level-1-undercroft" };
        var other = new MapDefinition { MapId = "level-2-crypt" };

        Assert.Empty(MapAudit.Unopenable("a.regions.json", map, new[] { ("a.regions.json", map), ("b.regions.json", other) }, Drawn));
        Assert.Contains("b.regions.json annotates a map with the same id", Assert.Single(MapAudit.Unopenable("a.regions.json", map, new[] { ("a.regions.json", map), ("b.regions.json", copy) }, Drawn)), StringComparison.Ordinal);
        Assert.Contains("No level-2-crypt.ds draws the map", Assert.Single(MapAudit.Unopenable("b.regions.json", other, new[] { ("b.regions.json", other) }, Drawn)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_region_with_no_id_a_shared_id_or_too_few_corners_is_reported()
    {
        var map = new MapDefinition
        {
            Regions = new[]
            {
                Rectangle("r-1", 0, 0, 10, 10),
                Rectangle(string.Empty, 10, 0, 20, 10),
                new Region { RegionId = "r-2", Label = "A line", Polygon = new[] { new MapPoint(0, 0), new MapPoint(5, 5) } },
                Rectangle("r-1", 20, 0, 30, 10),
            },
        };

        IReadOnlyList<string> faults = MapAudit.BadRegions(map);

        Assert.Equal(3, faults.Count);
        Assert.Contains("regions[1]", faults[0], StringComparison.Ordinal);
        Assert.Contains("'r-2' ('A line') has 2 corners", faults[1], StringComparison.Ordinal);
        Assert.Contains("Two regions have the id 'r-1'", faults[2], StringComparison.Ordinal);
    }

    [Fact]
    public void A_region_that_opens_an_area_opens_nothing_or_waits_for_its_floor_is_not_reported()
    {
        var map = new MapDefinition
        {
            Regions = new[]
            {
                Rectangle("landing", 0, 0, 10, 10, Landing),
                Rectangle("unlinked", 10, 0, 20, 10),
                Rectangle("crypt", 20, 0, 30, 10, "data_dossiers_level_2_area_1"),
            },
        };

        Assert.Empty(MapAudit.BadLinks(map, Floor(), FloorOneWritten));
    }

    [Theory]
    [InlineData("data_dossiers_level_1_area_9")]
    [InlineData("the-crypt")]
    [InlineData("data_dossiers_level_2")]
    public void A_region_that_opens_no_area_and_waits_for_no_floor_is_reported(string link)
    {
        var map = new MapDefinition { Regions = new[] { Rectangle("r-1", 0, 0, 10, 10, link) } };

        string fault = Assert.Single(MapAudit.BadLinks(map, Floor(), FloorOneWritten));

        Assert.Contains($"opens '{link}', which is no area a level file writes", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void An_area_on_a_mapped_floor_with_no_region_is_reported_unless_one_sharing_its_number_has_one()
    {
        FakeDossierStore dossiers = Floor();
        var map = new MapDefinition { Regions = new[] { Rectangle("landing", 0, 0, 10, 10, Landing), Rectangle("nave", 10, 0, 20, 10, Nave) } };

        string fault = Assert.Single(MapAudit.Unclickable(map, new[] { map }, dossiers));

        Assert.Contains("Workshop (Level 1, Area 4)", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void An_umbrella_area_whose_sub_areas_have_no_region_either_is_reported_with_them()
    {
        var map = new MapDefinition { Regions = new[] { Rectangle("landing", 0, 0, 10, 10, Landing) } };

        IReadOnlyList<string> faults = MapAudit.Unclickable(map, new[] { map }, Floor());

        Assert.Equal(3, faults.Count);
        Assert.Contains(faults, fault => fault.Contains("The Lower Nave (Level 1, Area 3)", StringComparison.Ordinal));
        Assert.Contains(faults, fault => fault.Contains("Nave (Level 1, Area 3a)", StringComparison.Ordinal));
    }

    [Fact]
    public void An_area_another_map_shows_can_be_clicked()
    {
        FakeDossierStore dossiers = Floor();
        var map = new MapDefinition { Regions = new[] { Rectangle("landing", 0, 0, 10, 10, Landing), Rectangle("nave", 10, 0, 20, 10, Nave) } };
        var workshop = new MapDefinition { Regions = new[] { Rectangle("workshop", 0, 0, 10, 10, "data_dossiers_level_1_area_4") } };

        Assert.Empty(MapAudit.Unclickable(map, new[] { map, workshop }, dossiers));
    }

    [Fact]
    public void A_map_that_shows_no_written_floor_has_no_area_to_miss()
    {
        var map = new MapDefinition { Regions = new[] { Rectangle("crypt", 0, 0, 10, 10, "data_dossiers_level_2_area_1") } };

        Assert.Empty(MapAudit.Unclickable(map, new[] { map }, Floor()));
    }

    [Fact]
    public void Regions_side_by_side_do_not_cover_each_other()
    {
        var map = new MapDefinition { Regions = new[] { Rectangle("west", 0, 0, 10, 10), Rectangle("east", 10, 0, 20, 10), Rectangle("south", 0, 10, 20, 20) } };

        Assert.Empty(MapAudit.Covered(map));
    }

    [Fact]
    public void A_region_drawn_over_another_is_reported()
    {
        // Off the hall's middle, so only the one direction is at fault.
        var map = new MapDefinition { Regions = new[] { Rectangle("hall", 0, 0, 30, 30), Rectangle("alcove", 2, 2, 8, 8) } };

        string fault = Assert.Single(MapAudit.Covered(map));

        Assert.Contains("'hall' covers the middle of 'alcove'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void An_l_shaped_room_whose_box_but_not_whose_outline_covers_a_neighbour_is_not_reported()
    {
        // The L runs along the top and down the left; the closet sits in the corner it leaves free. The
        // L's bounding box covers the closet's middle; the L itself, which is what a click tests, does not.
        var l = new Region
        {
            RegionId = "l",
            Label = "L",
            Polygon = new[] { new MapPoint(0, 0), new MapPoint(30, 0), new MapPoint(30, 10), new MapPoint(10, 10), new MapPoint(10, 30), new MapPoint(0, 30) },
        };
        var map = new MapDefinition { Regions = new[] { l, Rectangle("closet", 10, 10, 30, 30) } };

        Assert.Empty(MapAudit.Covered(map));
    }

    [Fact]
    public void A_u_shaped_room_is_tested_at_a_point_inside_it_not_at_its_boxs_middle()
    {
        // The U's box middle falls in its gap. A pillar standing in that gap covers the middle of the box,
        // not of the U, so it covers nothing; a pillar on one of its arms does.
        var u = new Region
        {
            RegionId = "u",
            Label = "U",
            Polygon = new[]
            {
                new MapPoint(0, 0), new MapPoint(10, 0), new MapPoint(10, 20), new MapPoint(20, 20), new MapPoint(20, 0),
                new MapPoint(30, 0), new MapPoint(30, 30), new MapPoint(0, 30),
            },
        };

        MapPoint inside = MapAudit.Inside(u);
        Assert.Equal("u", DungeonTable.Web.Services.MapFraming.RegionAt(new[] { u }, inside.X, inside.Y));

        Assert.Empty(MapAudit.Covered(new MapDefinition { Regions = new[] { u, Rectangle("gap", 12, 2, 18, 18) } }));
        Assert.Contains(MapAudit.Covered(new MapDefinition { Regions = new[] { u, Rectangle("arm", inside.X - 1, inside.Y - 1, inside.X + 1, inside.Y + 1) } }), fault => fault.Contains("'arm' covers the middle of 'U'", StringComparison.Ordinal));
    }

    private static Region Rectangle(string id, double left, double top, double right, double bottom, string link = "") => new()
    {
        RegionId = id,
        Label = id,
        GraphNodeId = link,
        Polygon = new[] { new MapPoint(left, top), new MapPoint(right, top), new MapPoint(right, bottom), new MapPoint(left, bottom) },
    };

    // Level 1: the landing, an umbrella area 3 that 3a fills, and the workshop.
    private static FakeDossierStore Floor()
    {
        var dossiers = new FakeDossierStore();
        var level = new LevelDossier { LevelNodeId = "data_dossiers_level_1" };
        foreach ((string id, string title, int number) in new[]
                 {
                     (Landing, "Landing (Level 1, Area 1)", 1),
                     ("data_dossiers_level_1_area_3", "The Lower Nave (Level 1, Area 3)", 3),
                     (Nave, "Nave (Level 1, Area 3a)", 3),
                     ("data_dossiers_level_1_area_4", "Workshop (Level 1, Area 4)", 4),
                 })
        {
            dossiers.Areas[id] = new AreaDossier { AreaNodeId = id, Title = title, AreaNumber = number };
            dossiers.LevelsByArea[id] = level;
        }

        dossiers.LevelNumbers["data_dossiers_level_1"] = 1;
        return dossiers;
    }
}
