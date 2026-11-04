using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Session;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// What is left of a creature an area lists: counted down one at a time, and, for one somebody or one
/// band listed in several rooms, the same somebody in each room, unless he fled.
/// </summary>
public sealed class CreatureStandingTests
{
    private const string Workshop = "area-workshop";
    private const string Vestry = "area-vestry";
    private const string Den = "area-den";
    private const string Hall = "area-hall";
    private const string Bugbear = "data_statblocks_monsters_bugbear";

    // Grask: one somebody, listed in the workshop and, if he flees, in the vestry.
    private static readonly EncounterMonster Grask = new EncounterMonster
    {
        NodeId = "grask", StatBlockNodeId = Bugbear, Name = "Grask", Individual = true, Shared = true,
    };

    // A kind of creature: the bugbears of one room are not the bugbears of the next.
    private static readonly EncounterMonster Bugbears = new EncounterMonster
    {
        NodeId = Bugbear, StatBlockNodeId = Bugbear, Name = "Bugbear", SuggestedCount = 4,
    };

    [Fact]
    public void A_kind_of_creature_counts_down_in_its_own_room_only()
    {
        CampaignState campaign = State();

        campaign.Count(Workshop, Bugbear, 1, 0);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Bugbears, campaign);
        Assert.Equal(4, workshop.Listed);
        Assert.Equal(1, workshop.DealtHere);
        Assert.Equal(3, workshop.Left);
        Assert.False(workshop.Gone);
        Assert.False(workshop.Shared);
        Assert.Equal(4, CreatureStanding.Of(Den, Bugbears, campaign).Left);
    }

    [Fact]
    public void A_listing_ticked_off_whole_is_gone_and_a_fled_one_counts_as_gone_from_the_room()
    {
        CampaignState campaign = State();

        campaign.ToggleCreatureCleared(Workshop, Bugbear);
        campaign.Count(Den, Bugbear, 1, 2);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Bugbears, campaign);
        Assert.True(workshop.Gone);
        Assert.Equal(4, workshop.DealtHere);
        Assert.False(workshop.GoneElsewhere);
        Assert.Equal(1, CreatureStanding.Of(Den, Bugbears, campaign).Left);
    }

    [Fact]
    public void One_somebody_dealt_with_in_one_room_is_dealt_with_in_every_room()
    {
        CampaignState campaign = State();

        campaign.Count(Workshop, Grask.NodeId, 1, 0);

        CreatureStanding vestry = CreatureStanding.Of(Vestry, Grask, campaign);
        Assert.True(vestry.Gone);
        Assert.True(vestry.GoneElsewhere);
        Assert.Equal(new[] { Workshop }, vestry.DealtIn);
        Assert.Equal(1, vestry.BandSize);
        Assert.Equal(0, vestry.BandLeft);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Grask, campaign);
        Assert.True(workshop.Gone);
        Assert.False(workshop.GoneElsewhere);
    }

    [Fact]
    public void One_somebody_ticked_off_in_an_older_build_is_dealt_with_everywhere_too()
    {
        // A tick from before the counts existed is a whole listing, which for one somebody is him.
        CampaignState campaign = State();

        campaign.ToggleCreatureCleared(Workshop, Grask.NodeId);

        Assert.True(CreatureStanding.Of(Vestry, Grask, campaign).GoneElsewhere);
    }

    [Fact]
    public void One_somebody_who_fled_is_gone_from_that_room_and_still_up_where_he_went()
    {
        CampaignState campaign = State();

        campaign.Count(Workshop, Grask.NodeId, 0, 1);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Grask, campaign);
        Assert.True(workshop.Gone);
        Assert.Equal(1, workshop.FledHere);
        Assert.False(workshop.GoneElsewhere);

        CreatureStanding vestry = CreatureStanding.Of(Vestry, Grask, campaign);
        Assert.False(vestry.Gone);
        Assert.Equal(1, vestry.Left);
        Assert.Equal(1, vestry.BandLeft);
    }

    [Fact]
    public void A_band_never_shows_more_in_a_room_than_it_has_left()
    {
        // Seven bandits: two at the gate, four in the barracks, and all seven again in the hall where
        // they gather. Two dealt with at the gate leave none there, the barracks' four, and five in the hall.
        CampaignState campaign = State();
        EncounterMonster gate = Bandits(2);
        EncounterMonster barracks = Bandits(4);
        EncounterMonster hall = Bandits(7);

        campaign.Count(Workshop, gate.NodeId, 2, 0);

        Assert.True(CreatureStanding.Of(Workshop, gate, campaign).Gone);
        Assert.Equal(4, CreatureStanding.Of(Den, barracks, campaign).Left);
        CreatureStanding gathered = CreatureStanding.Of(Hall, hall, campaign);
        Assert.Equal(5, gathered.Left);
        Assert.Equal(5, gathered.BandLeft);
        Assert.Equal(7, gathered.BandSize);

        // Four more in the barracks leave one: the hall says one, though the barracks listed none of him.
        campaign.Count(Den, barracks.NodeId, 4, 0);
        Assert.Equal(1, CreatureStanding.Of(Hall, hall, campaign).Left);

        // And the last one, wherever he falls, empties every room of them.
        campaign.Count(Hall, hall.NodeId, 1, 0);
        CreatureStanding emptied = CreatureStanding.Of(Hall, hall, campaign);
        Assert.True(emptied.Gone);
        Assert.Equal(1, emptied.DealtHere);
        Assert.True(emptied.GoneElsewhere);
        Assert.True(CreatureStanding.Of(Vestry, Bandits(1), campaign).GoneElsewhere);
    }

    [Fact]
    public void A_band_member_who_fled_is_not_one_fewer_anywhere_else()
    {
        CampaignState campaign = State();
        EncounterMonster gate = Bandits(2);

        campaign.Count(Workshop, gate.NodeId, 0, 1);

        Assert.Equal(1, CreatureStanding.Of(Workshop, gate, campaign).Left);
        Assert.Equal(7, CreatureStanding.Of(Hall, Bandits(7), campaign).Left);
    }

    [Fact]
    public void A_listing_of_more_than_the_band_widens_it_rather_than_hiding_what_the_room_lists()
    {
        CampaignState campaign = State();

        Assert.Equal(9, CreatureStanding.Of(Hall, Bandits(9), campaign).Left);
        Assert.Equal(3, CreatureStanding.Of(Hall, new EncounterMonster { NodeId = "twins", Shared = true, Individual = true, SuggestedCount = 3 }, campaign).Left);
    }

    [Fact]
    public void A_rolled_count_is_gone_with_anything_recorded_and_with_its_band()
    {
        CampaignState campaign = State();
        var goblins = new EncounterMonster { NodeId = "goblin", SuggestedCount = 4, CountExpression = "1d4+2" };
        var sentries = new EncounterMonster { NodeId = "sentries", Shared = true, BandSize = 3, SuggestedCount = 2, CountExpression = "1d3" };

        Assert.Equal(4, CreatureStanding.Of(Den, goblins, campaign).Left);
        Assert.True(CreatureStanding.Of(Den, goblins, campaign).Rolled);

        campaign.Count(Den, "goblin", 0, 1);
        Assert.True(CreatureStanding.Of(Den, goblins, campaign).Gone);

        Assert.False(CreatureStanding.Of(Hall, sentries, campaign).Gone);
        campaign.Count(Workshop, "sentries", 3, 0);
        Assert.True(CreatureStanding.Of(Hall, sentries, campaign).Gone);
    }

    [Fact]
    public void The_rows_still_up_carry_what_is_left_of_them()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, Bugbear, 3, 0);
        campaign.Count(Vestry, Grask.NodeId, 1, 0);

        IReadOnlyList<EncounterMonster> up = CreatureStanding.StillUp(Workshop, new[] { Grask, Bugbears }, campaign);

        EncounterMonster left = Assert.Single(up);
        Assert.Equal(Bugbear, left.NodeId);
        Assert.Equal(1, left.SuggestedCount);
        Assert.Equal("1", CreatureTally.Count(up));
    }

    // ---- The record behind it ---------------------------------------------------------------

    [Fact]
    public void Counts_never_go_below_nothing_and_an_area_that_records_nothing_is_not_kept()
    {
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.Count(Workshop, Bugbear, 2, 1);
        campaign.Count(Workshop, Bugbear, -5, -5);

        Assert.Equal(CreatureRecord.None, campaign.RecordOf(Workshop, Bugbear));
        Assert.Empty(campaign.Capture().Areas);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Taking_one_back_from_a_listing_ticked_off_whole_leaves_all_but_one_dealt_with()
    {
        CampaignState campaign = State();
        campaign.ToggleCreatureCleared(Workshop, Bugbear);

        campaign.TakeBackOne(Workshop, Bugbear, listed: 4);

        Assert.Equal(new CreatureRecord(false, 3, 0), campaign.RecordOf(Workshop, Bugbear));

        campaign.TakeBackOne(Workshop, Bugbear, listed: 4);
        Assert.Equal(2, campaign.RecordOf(Workshop, Bugbear).Dealt);
    }

    [Fact]
    public void Ticking_off_whole_or_putting_back_drops_the_counts()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, Bugbear, 1, 1);

        campaign.ToggleCreatureCleared(Workshop, Bugbear);
        Assert.Equal(new CreatureRecord(true, 0, 0), campaign.RecordOf(Workshop, Bugbear));

        campaign.Count(Workshop, Bugbear, 1, 0);
        Assert.Equal(new CreatureRecord(true, 0, 0), campaign.RecordOf(Workshop, Bugbear));

        campaign.PutBack(Workshop, Bugbear);
        Assert.Equal(CreatureRecord.None, campaign.RecordOf(Workshop, Bugbear));
    }

    [Fact]
    public void Putting_back_everywhere_forgets_what_was_dealt_with_and_keeps_who_fled()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, Grask.NodeId, 0, 1);
        campaign.Count(Vestry, Grask.NodeId, 1, 0);
        campaign.ToggleCreatureCleared(Den, Grask.NodeId);
        campaign.Count(Hall, Bugbear, 1, 0);

        campaign.PutBackEverywhere(Grask.NodeId);

        Assert.Empty(campaign.DealtByArea(Grask.NodeId));
        Assert.Equal(new CreatureRecord(false, 0, 1), campaign.RecordOf(Workshop, Grask.NodeId));
        Assert.Equal(1, campaign.RecordOf(Hall, Bugbear).Dealt);
    }

    [Fact]
    public void Where_one_somebody_was_dealt_with_counts_each_room_once()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, "bandit-gang", 2, 1);
        campaign.ToggleCreatureCleared(Den, "bandit-gang");

        Dictionary<string, int> dealt = campaign.DealtByArea("bandit-gang").ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Equal(2, dealt[Workshop]);
        Assert.Equal(1, dealt[Den]);
        Assert.Equal(2, dealt.Count);
    }

    [Fact]
    public void The_counts_are_saved_and_restored_once_each()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, Bugbear, 2, 1);

        CampaignSnapshot saved = campaign.Capture();
        CreatureCount count = Assert.Single(Assert.Single(saved.Areas).CreatureCounts);
        Assert.Equal(Bugbear, count.CreatureId);

        // A hand-edited document: the same creature twice, a negative count, and an entry of nothing.
        saved.Areas[0].CreatureCounts = new[]
        {
            new CreatureCount { CreatureId = Bugbear, Dealt = 2, Fled = -1 },
            new CreatureCount { CreatureId = Bugbear, Dealt = 9, Fled = 9 },
            new CreatureCount { CreatureId = "grask" },
            new CreatureCount { CreatureId = " ", Dealt = 1 },
        };

        CampaignState restored = State();
        restored.Restore(saved);

        Assert.Equal(new CreatureRecord(false, 2, 0), restored.RecordOf(Workshop, Bugbear));
        Assert.Single(Assert.Single(restored.Capture().Areas).CreatureCounts);
    }

    private static EncounterMonster Bandits(int count) => new EncounterMonster
    {
        NodeId = "bandit-gang", StatBlockNodeId = "data_statblocks_monsters_bandit", Name = "Gang bandit",
        SuggestedCount = count, Shared = true, BandSize = 7,
    };

    private static CampaignState State() => new CampaignState(new FakePartyRoster());
}
