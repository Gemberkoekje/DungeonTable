using System.Collections.Generic;
using DungeonTable.ContentTests.Rules;
using DungeonTable.Core.Dossier;

namespace DungeonTable.ContentTests.RuleTests;

/// <summary>Proves the campaign rules catch a start area that is not there and a level table that is wrong.</summary>
public sealed class CampaignAuditTests
{
    private static readonly HashSet<int> FloorOneWritten = new() { 1 };

    private static readonly int[] FloorOne = { 1 };

    [Theory]
    [InlineData("")]
    [InlineData("landing")]
    [InlineData("  landing ")]
    public void A_start_area_left_out_or_written_is_not_reported(string start)
    {
        Assert.Empty(CampaignAudit.MissingStartArea(new CampaignDossier { StartAreaNodeId = start }, Landing()));
    }

    [Fact]
    public void A_start_area_no_level_file_writes_is_reported()
    {
        string fault = Assert.Single(CampaignAudit.MissingStartArea(new CampaignDossier { StartAreaNodeId = "landign" }, Landing()));

        Assert.Contains("'landign'", fault, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_row_with_its_name_and_character_level_is_not_reported_with_or_without_a_required_level()
    {
        var campaign = new CampaignDossier
        {
            Levels = new[]
            {
                new LevelSummary { Level = 1, Name = "The Undercroft", CharacterLevel = "20th", RequiredLevel = 20 },
                new LevelSummary { Level = 2, Name = "The Sealed Crypt", CharacterLevel = "3rd" },
            },
        };

        Assert.Empty(CampaignAudit.IncompleteLevelRows(campaign));
    }

    [Fact]
    public void A_level_row_with_no_name_no_character_level_or_an_impossible_required_level_is_reported()
    {
        var campaign = new CampaignDossier
        {
            Levels = new[]
            {
                new LevelSummary { Level = 1, CharacterLevel = "2nd" },
                new LevelSummary { Level = 2, Name = "The Sealed Crypt" },
                new LevelSummary { Level = 3, Name = "The Deep", CharacterLevel = "21st", RequiredLevel = 21 },
            },
        };

        IReadOnlyList<string> faults = CampaignAudit.IncompleteLevelRows(campaign);

        Assert.Equal(3, faults.Count);
        Assert.Contains("levels[0] (level 1) has no name", faults[0], StringComparison.Ordinal);
        Assert.Contains("levels[1] (level 2) gives no character level", faults[1], StringComparison.Ordinal);
        Assert.Contains("requires character level 21", faults[2], StringComparison.Ordinal);
    }

    [Fact]
    public void An_authored_mark_that_matches_the_level_files_is_not_reported()
    {
        var campaign = new CampaignDossier
        {
            Levels = new[]
            {
                new LevelSummary { Level = 1, Name = "The Undercroft", Authored = true },
                new LevelSummary { Level = 2, Name = "The Sealed Crypt" },
                new LevelSummary { Level = 0, Name = "The village below", Authored = true },
            },
        };

        Assert.Empty(CampaignAudit.WrongAuthoredMarks(campaign, FloorOneWritten));
    }

    [Fact]
    public void An_authored_mark_the_level_files_contradict_is_reported_both_ways()
    {
        var campaign = new CampaignDossier
        {
            Levels = new[]
            {
                new LevelSummary { Level = 1, Name = "The Undercroft" },
                new LevelSummary { Level = 2, Name = "The Sealed Crypt", Authored = true },
            },
        };

        IReadOnlyList<string> faults = CampaignAudit.WrongAuthoredMarks(campaign, FloorOneWritten);

        Assert.Equal(2, faults.Count);
        Assert.Contains("Level 1 ('The Undercroft') has a level-1.json, but its row is not marked authored", faults[0], StringComparison.Ordinal);
        Assert.Contains("Level 2 ('The Sealed Crypt') is marked authored, but no level-2.json is loaded", faults[1], StringComparison.Ordinal);
    }

    [Fact]
    public void The_floors_with_a_file_are_the_numbered_levels_the_store_loaded()
    {
        FakeDossierStore dossiers = Landing();
        dossiers.Levels.Add(new LevelDossier { LevelNodeId = "unnumbered" });

        Assert.Equal(FloorOne, CampaignAudit.FloorsWithAFile(dossiers));
    }

    private static FakeDossierStore Landing()
    {
        var dossiers = new FakeDossierStore();
        var level = new LevelDossier { LevelNodeId = "floor-1" };
        dossiers.Areas["landing"] = new AreaDossier { AreaNodeId = "landing", Title = "Landing (Level 1, Area 1)" };
        dossiers.LevelsByArea["landing"] = level;
        dossiers.LevelNumbers["floor-1"] = 1;
        return dossiers;
    }
}
