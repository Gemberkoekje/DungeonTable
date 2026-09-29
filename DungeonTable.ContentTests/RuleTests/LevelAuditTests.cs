using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;
using DungeonTable.Infrastructure.Links;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>Proves the level-file rules catch a floor or an area the app could not place or reach.</summary>
public sealed class LevelAuditTests
{
    private const string Floor = "data_dossiers_level_1";

    [Fact]
    public void A_numbered_file_that_names_its_floor_and_every_area_is_placed()
    {
        Assert.Empty(LevelAudit.Unplaced("level-1.json", Set(Area("data_dossiers_level_1_area_1", "Landing (Level 1, Area 1)"))));
    }

    [Theory]
    [InlineData("level-one.json")]
    [InlineData("level-0.json")]
    [InlineData("level1.json")]
    public void A_file_with_no_floor_number_is_reported(string fileName)
    {
        string fault = Assert.Single(LevelAudit.Unplaced(fileName, Set()));

        Assert.Contains("level-<n>.json", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_with_no_id_and_an_area_with_none_are_reported()
    {
        var set = new LevelDossierSet
        {
            Level = new LevelDossier { Title = "Level 1" },
            Areas = new[] { Area("data_dossiers_level_1_area_1", "Landing (Level 1, Area 1)"), Area(string.Empty, "Nave (Level 1, Area 3a)") },
        };

        IReadOnlyList<string> faults = LevelAudit.Unplaced("level-1.json", set);

        Assert.Equal(2, faults.Count);
        Assert.Contains("levelNodeId", faults[0], StringComparison.Ordinal);
        Assert.Contains("areas[1] ('Nave (Level 1, Area 3a)') has no areaNodeId", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void An_id_written_twice_in_a_file_or_in_two_files_is_reported()
    {
        LevelDossierSet one = Set(Area("a-1", "Landing (Level 1, Area 1)"), Area("a-1", "Landing again (Level 1, Area 1)"), Area("a-2", "Den (Level 1, Area 2)"));
        var two = new LevelDossierSet
        {
            Level = new LevelDossier { LevelNodeId = Floor },
            Areas = new[] { Area("a-2", "Den (Level 2, Area 2)") },
        };

        IReadOnlyList<string> faults = LevelAudit.WrittenTwice("level-1.json", one, new[] { ("level-1.json", one), ("level-2.json", two) });

        Assert.Equal(3, faults.Count);
        Assert.Contains(faults, fault => fault.Contains("level-2.json declares the same level id", StringComparison.Ordinal));
        Assert.Contains(faults, fault => fault.Contains("'a-1' is written more than once", StringComparison.Ordinal));
        Assert.Contains(faults, fault => fault.Contains("'a-2' is also in level-2.json", StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_is_not_its_own_duplicate()
    {
        LevelDossierSet one = Set(Area("a-1", "Landing (Level 1, Area 1)"));

        Assert.Empty(LevelAudit.WrittenTwice("level-1.json", one, new[] { ("level-1.json", one) }));
    }

    [Fact]
    public void An_area_reached_by_its_printed_key_or_by_its_number_is_not_reported()
    {
        LevelDossierSet set = Set(
            Area("data_dossiers_level_1_area_3a", "Nave (Level 1, Area 3a)"),
            new AreaDossier { AreaNodeId = "data_dossiers_level_1_area_4", Title = "Workshop", AreaNumber = 4 });

        Assert.Empty(LevelAudit.Unreachable(set, Targets(set)));
    }

    [Fact]
    public void An_area_with_no_key_or_one_it_shares_is_reported()
    {
        LevelDossierSet set = Set(
            new AreaDossier { AreaNodeId = "keyless", Title = "A room with no number" },
            Area("vestry-1", "Vestry (Level 1, Area 3b)"),
            Area("vestry-2", "Old vestry (Level 1, Area 3b)"));

        IReadOnlyList<string> faults = LevelAudit.Unreachable(set, Targets(set));

        Assert.Equal(3, faults.Count);
        Assert.Contains("'keyless' has no key", faults[0], StringComparison.Ordinal);
        Assert.Contains("[[area 3b]] does not reach the area 'vestry-1'", faults[1], StringComparison.Ordinal);
        Assert.Contains("ambiguous", faults[2], StringComparison.Ordinal);
    }

    [Fact]
    public void An_area_whose_key_reaches_a_different_area_is_reported()
    {
        // The file says area 2 is 'den', but what the app loaded as area 2 on this floor is another area,
        // as when a later file's area of the same id took this one's place.
        LevelDossierSet written = Set(Area("den", "Goblin Den (Level 1, Area 2)"));
        LevelDossierSet loaded = Set(Area("den-2", "Goblin Den (Level 1, Area 2)"));

        string fault = Assert.Single(LevelAudit.Unreachable(written, Targets(loaded)));

        Assert.Contains("[[area 2]] reaches 'den-2', not the area 'den'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_floor_with_no_id_is_left_to_the_placing_rule()
    {
        var set = new LevelDossierSet { Level = new LevelDossier(), Areas = new[] { Area("a-1", "Landing (Level 1, Area 1)") } };

        Assert.Empty(LevelAudit.Unreachable(set, Targets(Set())));
    }

    [Fact]
    public void An_area_the_panel_cannot_open_is_reported()
    {
        LevelDossierSet loaded = Set(Area("data_dossiers_level_1_area_1", "Landing (Level 1, Area 1)"));
        LevelDossierSet written = Set(Area("data_dossiers_level_1_area_1", "Landing (Level 1, Area 1)"), Area("dropped", "Den (Level 1, Area 2)"));
        FakeDossierStore dossiers = Store(loaded);
        var stats = new FakeStatLibrary();
        var projection = new AuthoredProjection(dossiers, stats, LinkTargets.Build(dossiers, stats));

        string fault = Assert.Single(LevelAudit.Unbriefed(written, projection));

        Assert.Contains("'dropped' opens no briefing", fault, StringComparison.Ordinal);
    }

    private static AreaDossier Area(string id, string title) => new() { AreaNodeId = id, Title = title };

    private static LevelDossierSet Set(params AreaDossier[] areas) => new()
    {
        Level = new LevelDossier { LevelNodeId = Floor, Title = "Level 1: The Undercroft" },
        Areas = areas,
    };

    private static FakeDossierStore Store(LevelDossierSet set)
    {
        var dossiers = new FakeDossierStore();
        dossiers.Levels.Add(set.Level);
        dossiers.LevelNumbers[Floor] = 1;
        foreach (AreaDossier area in set.Areas)
        {
            dossiers.Areas[area.AreaNodeId] = area;
            dossiers.LevelsByArea[area.AreaNodeId] = set.Level;
        }

        return dossiers;
    }

    private static LinkTargets Targets(LevelDossierSet set) => LinkTargets.Build(Store(set), new FakeStatLibrary());
}
