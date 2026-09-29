using System.Collections.Generic;
using DungeonTable.Core.Briefing;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Stats;
using DungeonTable.Infrastructure.Links;
using DungeonTable.Tests.Web;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Battles;

/// <summary>
/// Verifies the encounter list an area produces: exactly the creatures it lists, with the authored
/// counts, one row per creature, and an individual as its own row filled from the block it uses.
/// </summary>
public sealed class EncounterSourceTests
{
    private const string Bugbear = "data_statblocks_monsters_bugbear";
    private const string Goblin = "data_statblocks_monsters_goblin";

    [Fact]
    public void An_area_that_lists_nobody_has_no_encounter()
    {
        EncounterSource source = Build();

        Assert.Empty(source.For(new RoomBriefing()));
        Assert.Empty(source.For(null));
    }

    [Fact]
    public void An_areas_creatures_are_its_encounter_with_their_counts()
    {
        EncounterSource source = Build();
        var briefing = Listing(
            new OccupantEntry { NodeId = Bugbear, MonsterRef = Bugbear, Label = "Bugbear", Count = "4", Note = "behind the pillars" },
            new OccupantEntry { NodeId = Goblin, MonsterRef = Goblin, Label = "Goblin", Count = "1d4+1" });

        IReadOnlyList<EncounterMonster> rows = source.For(briefing);

        Assert.Equal(2, rows.Count);
        Assert.Equal(Bugbear, rows[0].NodeId);
        Assert.Equal(Bugbear, rows[0].StatBlockNodeId);
        Assert.Equal("Bugbear", rows[0].Name);
        Assert.Equal(4, rows[0].SuggestedCount);
        Assert.Equal("behind the pillars", rows[0].Context);
        Assert.Equal("SRD 5.1 p.266", rows[0].Citation);
        Assert.True(rows[0].HasStatBlock);
        Assert.False(rows[0].Individual);

        Assert.Equal(1, rows[1].SuggestedCount);
        Assert.Equal("1d4+1", rows[1].CountExpression);
    }

    [Fact]
    public void A_creature_whose_block_is_not_extracted_is_still_offered_and_flagged()
    {
        EncounterSource source = Build();
        var briefing = Listing(new OccupantEntry { NodeId = "data_statblocks_monsters_grell", MonsterRef = "data_statblocks_monsters_grell", Label = "Grell", Count = "2" });

        EncounterMonster row = Assert.Single(source.For(briefing));

        Assert.False(row.HasStatBlock);
        Assert.Equal("Grell", row.Name);
        Assert.Equal(2, row.SuggestedCount);
    }

    [Fact]
    public void A_named_individual_is_its_own_row_filled_from_the_block_it_uses()
    {
        EncounterSource source = Build();
        var briefing = Listing(
            new OccupantEntry { NodeId = "grukk", MonsterRef = Bugbear, Label = "Grukk the Chief", Secret = true },
            new OccupantEntry { NodeId = "wenna-brask", Label = "Wenna Brask" });

        IReadOnlyList<EncounterMonster> rows = source.For(briefing);

        EncounterMonster grukk = rows[0];
        Assert.Equal("grukk", grukk.NodeId);
        Assert.Equal(Bugbear, grukk.StatBlockNodeId);
        Assert.Equal("Grukk the Chief", grukk.Name);
        Assert.True(grukk.Individual);
        Assert.True(grukk.HasStatBlock);
        Assert.True(grukk.Secret);
        Assert.Equal("SRD 5.1 p.266", grukk.Citation);

        EncounterMonster wenna = rows[1];
        Assert.True(wenna.Individual);
        Assert.Equal(string.Empty, wenna.StatBlockNodeId);
        Assert.False(wenna.HasStatBlock);
        Assert.Equal(string.Empty, wenna.Citation);
    }

    [Fact]
    public void A_creature_listed_twice_is_one_row_with_the_counts_added_and_both_notes()
    {
        EncounterSource source = Build();
        var briefing = Listing(
            new OccupantEntry { NodeId = Bugbear, MonsterRef = Bugbear, Label = "Bugbear", Count = "4", Note = "at the table", Secret = true },
            new OccupantEntry { NodeId = Bugbear, MonsterRef = Bugbear, Label = "Bugbear", Count = "2", Note = "by the door" });

        EncounterMonster row = Assert.Single(source.For(briefing));

        Assert.Equal(6, row.SuggestedCount);
        Assert.Equal("at the table · by the door", row.Context);
        Assert.False(row.Secret);
    }

    [Fact]
    public void A_dice_count_is_not_added_to_anything()
    {
        EncounterSource source = Build();
        var briefing = Listing(
            new OccupantEntry { NodeId = Goblin, MonsterRef = Goblin, Label = "Goblin", Count = "1d4" },
            new OccupantEntry { NodeId = Goblin, MonsterRef = Goblin, Label = "Goblin", Count = "3" });

        EncounterMonster row = Assert.Single(source.For(briefing));

        Assert.Equal("1d4", row.CountExpression);
        Assert.Equal(1, row.SuggestedCount);
    }

    [Fact]
    public void A_creature_whose_reference_names_nothing_is_left_out()
    {
        EncounterSource source = Build();
        var briefing = Listing(
            new OccupantEntry { Label = "bugbar", Count = "2" },
            new OccupantEntry { NodeId = Goblin, MonsterRef = Goblin, Label = "Goblin", Count = "banana" });

        EncounterMonster row = Assert.Single(source.For(briefing));

        Assert.Equal(Goblin, row.NodeId);
        Assert.Equal(1, row.SuggestedCount);
    }

    [Fact]
    public void An_authored_note_shows_its_links_words()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters[Bugbear] = Block(Bugbear, "Bugbear", 27, 16);
        var source = new EncounterSource(new MarkupCrossRefResolver(LinkTargets.Build(new FakeDossierStore(), stats)), Projection(), stats);
        var briefing = Listing(new OccupantEntry { NodeId = Bugbear, MonsterRef = Bugbear, Label = "Bugbear", Note = "guarding the [[bugbear|chief]]" });

        Assert.Equal("guarding the chief", Assert.Single(source.For(briefing)).Context);
    }

    private static RoomBriefing Listing(params OccupantEntry[] occupants) =>
        new RoomBriefing { Occupants = occupants };

    private static EncounterSource Build()
    {
        var stats = new FakeStatLibrary();
        stats.Monsters[Bugbear] = Block(Bugbear, "Bugbear", 27, 16);
        stats.Monsters[Goblin] = Block(Goblin, "Goblin", 7, 15);

        return new EncounterSource(new ScriptedCrossRefResolver(), Projection(), stats);
    }

    private static FakeContentProjection Projection()
    {
        var projection = new FakeContentProjection();
        projection.Cards[Bugbear] = new ReferenceCard
        {
            NodeId = Bugbear,
            Label = "Bugbear",
            Kind = CrossRefKind.Monster,
            Citation = "SRD 5.1 p.266",
        };

        projection.Cards[Goblin] = new ReferenceCard
        {
            NodeId = Goblin,
            Label = "Goblin",
            Kind = CrossRefKind.Monster,
            Citation = "SRD 5.1 p.315",
        };

        return projection;
    }

    private static StatBlock Block(string nodeId, string name, int hp, int ac) => new StatBlock
    {
        NodeId = nodeId,
        Name = name,
        AverageHitPoints = hp,
        ArmourClass = ac,
        Abilities = new AbilityScores { Str = 10, Dex = 14, Con = 10, Intelligence = 10, Wis = 10, Cha = 10 },
    };
}
