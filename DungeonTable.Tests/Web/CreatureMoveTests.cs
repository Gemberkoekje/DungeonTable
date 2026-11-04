using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Session;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Web;

/// <summary>
/// Creatures the DM moves to another room: one somebody is then in that room and nowhere else, a kind
/// of creature or a band moves by count, the room they go to need not list them, and every move can be
/// taken back.
/// </summary>
public sealed class CreatureMoveTests
{
    private const string Workshop = "area-workshop";
    private const string Vestry = "area-vestry";
    private const string Den = "area-den";
    private const string Hall = "area-hall";
    private const string Bugbear = "data_statblocks_monsters_bugbear";

    private static readonly string[] DenOnly = { Den };

    private static readonly string[] GraskListings = { Workshop, Vestry };

    // Grask: one somebody, listed in the workshop and, if he flees, in the vestry. Never in the den.
    private static readonly EncounterMonster Grask = new EncounterMonster
    {
        NodeId = "grask", StatBlockNodeId = Bugbear, Name = "Grask", Individual = true, Shared = true,
    };

    // A kind of creature, four to a room that lists them.
    private static readonly EncounterMonster Bugbears = new EncounterMonster
    {
        NodeId = Bugbear, StatBlockNodeId = Bugbear, Name = "Bugbear", SuggestedCount = 4,
    };

    [Fact]
    public void One_somebody_moved_to_a_room_that_does_not_list_him_is_there_and_gone_from_every_room_that_does()
    {
        CampaignState campaign = State();

        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);

        CreatureStanding den = CreatureStanding.Of(Den, Grask.Moved(1), campaign);
        Assert.Equal(1, den.Left);
        Assert.Equal(Workshop, den.MovedFrom);
        Assert.Equal(1, den.MovedHere);
        Assert.False(den.MovedElsewhere);

        foreach (string listing in GraskListings)
        {
            CreatureStanding there = CreatureStanding.Of(listing, Grask, campaign);
            Assert.True(there.Gone);
            Assert.True(there.MovedElsewhere);
            Assert.Equal(Den, there.MovedTo);
            Assert.False(there.GoneElsewhere);
            Assert.Equal(0, there.DealtHere);
        }
    }

    [Fact]
    public void Only_the_last_move_of_one_somebody_says_where_he_is()
    {
        CampaignState campaign = State();

        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);
        campaign.Move(Moved(Grask, Den, Hall), 0, oneSomebody: true);

        Assert.Equal(1, CreatureStanding.Of(Hall, Grask.Moved(1), campaign).Left);
        Assert.True(CreatureStanding.Of(Den, Grask.Moved(1), campaign).Gone);
        Assert.Equal(Hall, CreatureStanding.Of(Workshop, Grask, campaign).MovedTo);
    }

    [Fact]
    public void One_somebody_dealt_with_where_he_was_moved_is_dealt_with_in_every_room()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);

        campaign.Count(Den, Grask.NodeId, 1, 0);

        CreatureStanding vestry = CreatureStanding.Of(Vestry, Grask, campaign);
        Assert.True(vestry.Gone);
        Assert.True(vestry.GoneElsewhere);
        Assert.False(vestry.MovedElsewhere);
        Assert.Equal(DenOnly, vestry.DealtIn);
        Assert.Equal(1, CreatureStanding.Of(Den, Grask.Moved(1), campaign).DealtHere);
    }

    [Fact]
    public void One_somebody_who_flees_from_where_he_was_moved_is_loose_again_where_the_book_lists_him()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);

        campaign.Count(Den, Grask.NodeId, 0, 1);

        Assert.True(CreatureStanding.Of(Den, Grask.Moved(1), campaign).Gone);
        CreatureStanding vestry = CreatureStanding.Of(Vestry, Grask, campaign);
        Assert.Equal(1, vestry.Left);
        Assert.False(vestry.MovedElsewhere);
    }

    [Fact]
    public void One_somebody_moved_back_into_a_room_he_fled_from_is_back_in_it()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, Grask.NodeId, 0, 1);

        campaign.Move(Moved(Grask, Vestry, Workshop), 1, oneSomebody: true);

        Assert.Equal(1, CreatureStanding.Of(Workshop, Grask, campaign).Left);
        Assert.Equal(0, campaign.RecordOf(Workshop, Grask.NodeId).Fled);
        Assert.True(CreatureStanding.Of(Vestry, Grask, campaign).MovedElsewhere);
    }

    [Fact]
    public void Some_of_a_kind_moved_away_leave_the_rest_and_join_the_room_they_go_to()
    {
        CampaignState campaign = State();

        campaign.Move(Moved(Bugbears, Workshop, Den, 3), 4, oneSomebody: false);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Bugbears, campaign);
        Assert.Equal(1, workshop.Listed);
        Assert.Equal(1, workshop.Left);
        Assert.Equal(3, workshop.MovedAway);
        Assert.Equal(Den, workshop.MovedTo);
        Assert.False(workshop.MovedElsewhere);

        CreatureStanding den = CreatureStanding.Of(Den, Bugbears, campaign);
        Assert.Equal(7, den.Listed);
        Assert.Equal(7, den.Left);
        Assert.Equal(3, den.MovedHere);
        Assert.Equal(Workshop, den.MovedFrom);

        CreatureStanding hall = CreatureStanding.Of(Hall, Bugbears.Moved(3), campaign);
        Assert.True(hall.Gone);
        Assert.Equal(0, hall.Listed);
    }

    [Fact]
    public void A_kind_all_moved_away_reads_moved_not_dealt_with()
    {
        CampaignState campaign = State();

        campaign.Move(Moved(Bugbears, Workshop, Hall, 4), 0, oneSomebody: false);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Bugbears, campaign);
        Assert.True(workshop.Gone);
        Assert.True(workshop.MovedElsewhere);
        Assert.Equal(0, workshop.DealtHere);
        Assert.Equal(4, CreatureStanding.Of(Hall, Bugbears.Moved(4), campaign).Left);
    }

    [Fact]
    public void A_room_where_some_fell_and_the_rest_were_moved_reads_dealt_with()
    {
        CampaignState campaign = State();
        campaign.Count(Workshop, Bugbear, 1, 0);

        campaign.Move(Moved(Bugbears, Workshop, Hall, 3), 0, oneSomebody: false);

        CreatureStanding workshop = CreatureStanding.Of(Workshop, Bugbears, campaign);
        Assert.True(workshop.Gone);
        Assert.False(workshop.MovedElsewhere);
        Assert.Equal(1, workshop.DealtHere);
        Assert.Equal(3, workshop.MovedAway);

        // Putting the fallen one back leaves the moved three where they went.
        campaign.PutBack(Workshop, Bugbear);
        Assert.Equal(1, CreatureStanding.Of(Workshop, Bugbears, campaign).Left);
        Assert.Equal(3, CreatureStanding.Of(Hall, Bugbears.Moved(3), campaign).Left);
    }

    [Fact]
    public void Moving_into_a_room_ticked_off_whole_keeps_its_dead_dead_and_the_newcomers_up()
    {
        CampaignState campaign = State();
        campaign.ToggleCreatureCleared(Den, Bugbear);

        campaign.Move(Moved(Bugbears, Workshop, Den, 2), 4, oneSomebody: false);

        CreatureStanding den = CreatureStanding.Of(Den, Bugbears, campaign);
        Assert.Equal(2, den.Left);
        Assert.Equal(4, den.DealtHere);
        Assert.False(campaign.RecordOf(Den, Bugbear).Whole);
    }

    [Fact]
    public void A_whole_tick_counts_what_had_already_moved_in_and_out_before_the_next_move()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Bugbears, Workshop, Den, 3), 4, oneSomebody: false);
        campaign.Move(Moved(Bugbears, Den, Hall, 1), 0, oneSomebody: false);
        campaign.ToggleCreatureCleared(Den, Bugbear);

        campaign.Move(Moved(Bugbears, Vestry, Den, 2), 4, oneSomebody: false);

        // Four listed, three in, one out: six were there and were dealt with; the two newcomers are up.
        Assert.Equal(6, campaign.RecordOf(Den, Bugbear).Dealt);
        Assert.Equal(2, CreatureStanding.Of(Den, Bugbears, campaign).Left);
    }

    [Fact]
    public void Band_members_move_by_count_and_the_band_still_caps_every_room()
    {
        CampaignState campaign = State();
        EncounterMonster gate = Bandits(2);

        campaign.Move(Moved(gate, Workshop, Den, 2), 0, oneSomebody: false);
        campaign.Count(Hall, gate.NodeId, 6, 0);

        Assert.True(CreatureStanding.Of(Workshop, gate, campaign).MovedElsewhere);
        CreatureStanding den = CreatureStanding.Of(Den, gate.Moved(2), campaign);
        Assert.Equal(1, den.Left);
        Assert.Equal(1, den.BandLeft);
    }

    [Fact]
    public void A_move_can_be_taken_back_from_either_room()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Bugbears, Workshop, Den, 3), 4, oneSomebody: false);

        campaign.UndoMove(Bugbear, Den, into: true);
        Assert.Equal(4, CreatureStanding.Of(Workshop, Bugbears, campaign).Left);
        Assert.Empty(campaign.MovesOf(Bugbear));

        campaign.Move(Moved(Bugbears, Workshop, Den, 3), 4, oneSomebody: false);
        campaign.UndoMove(Bugbear, Workshop, into: false);
        Assert.Equal(4, CreatureStanding.Of(Den, Bugbears, campaign).Left);
        Assert.Empty(campaign.MovesOf(Bugbear));
    }

    [Fact]
    public void Taking_back_a_move_takes_back_only_the_last_one_that_matches()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);
        campaign.Move(Moved(Grask, Den, Hall), 0, oneSomebody: true);

        campaign.UndoMove(Grask.NodeId, Hall, into: true);

        Assert.Equal(Den, Assert.Single(campaign.MovesOf(Grask.NodeId)).ToAreaNodeId);
        Assert.Equal(1, CreatureStanding.Of(Den, Grask.Moved(1), campaign).Left);
    }

    [Fact]
    public void Taking_back_the_move_into_a_room_that_had_two_takes_back_the_later()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Bugbears, Workshop, Den, 2), 4, oneSomebody: false);
        campaign.Move(Moved(Bugbears, Vestry, Den, 1), 4, oneSomebody: false);

        campaign.UndoMove(Bugbear, Den, into: true);

        Assert.Equal(Workshop, Assert.Single(campaign.MovesOf(Bugbear)).FromAreaNodeId);
        Assert.Equal(4, CreatureStanding.Of(Vestry, Bugbears, campaign).Left);
    }

    [Fact]
    public void Forgetting_one_somebodys_moves_puts_him_where_the_book_lists_him()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);
        campaign.Move(Moved(Grask, Den, Hall), 0, oneSomebody: true);
        campaign.Move(Moved(Bugbears, Workshop, Den, 1), 4, oneSomebody: false);

        campaign.ForgetMoves(Grask.NodeId, Vestry, oneSomebody: true);

        Assert.Empty(campaign.MovesOf(Grask.NodeId));
        Assert.Equal(1, CreatureStanding.Of(Workshop, Grask, campaign).Left);
        Assert.Single(campaign.MovesOf(Bugbear));
    }

    [Fact]
    public void Forgetting_a_kinds_moves_out_of_a_room_brings_them_back_to_it_only()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Bugbears, Workshop, Den, 2), 4, oneSomebody: false);
        campaign.Move(Moved(Bugbears, Vestry, Den, 1), 4, oneSomebody: false);

        campaign.ForgetMoves(Bugbear, Workshop, oneSomebody: false);

        Assert.Equal(4, CreatureStanding.Of(Workshop, Bugbears, campaign).Left);
        Assert.Equal(Vestry, Assert.Single(campaign.MovesOf(Bugbear)).FromAreaNodeId);
    }

    [Fact]
    public void A_move_that_says_nothing_is_ignored_and_one_somebody_moves_whole()
    {
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.Move(Moved(Bugbears, Workshop, Workshop, 1), 4, oneSomebody: false);
        campaign.Move(Moved(Bugbears, Workshop, Den, 0), 4, oneSomebody: false);
        campaign.Move(Moved(Bugbears, string.Empty, Den, 1), 4, oneSomebody: false);
        campaign.Move(null, 4, oneSomebody: false);
        Assert.Equal(0, changes);

        campaign.Move(Moved(Grask, Workshop, Den, 5), 0, oneSomebody: true);
        Assert.Equal(1, Assert.Single(campaign.MovesOf(Grask.NodeId)).Count);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void A_move_remembers_the_room_that_lists_the_creature()
    {
        CampaignState campaign = State();

        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);
        campaign.Move(new CreatureMove { CreatureId = Grask.NodeId, FromAreaNodeId = Den, ToAreaNodeId = Hall, Count = 1, ListedInAreaNodeId = Workshop }, 0, oneSomebody: true);

        Assert.All(campaign.MovesOf(Grask.NodeId), move => Assert.Equal(Workshop, move.ListedInAreaNodeId));
        Assert.Equal(Hall, Assert.Single(campaign.MovesInto(Hall)).ToAreaNodeId);
    }

    [Fact]
    public void Moves_survive_a_restart_and_a_move_that_says_nothing_does_not()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);
        campaign.Move(Moved(Bugbears, Workshop, Hall, 3), 0, oneSomebody: false);
        CampaignSnapshot saved = campaign.Capture();
        saved.Moves = saved.Moves.Concat(new[]
        {
            null,
            new CreatureMove { CreatureId = " ", FromAreaNodeId = Workshop, ToAreaNodeId = Den, Count = 1 },
            new CreatureMove { CreatureId = Bugbear, FromAreaNodeId = Den, ToAreaNodeId = Den, Count = 1 },
            new CreatureMove { CreatureId = Bugbear, FromAreaNodeId = Den, ToAreaNodeId = Hall, Count = 0 },
            new CreatureMove { CreatureId = Bugbear, FromAreaNodeId = Vestry, ToAreaNodeId = Hall, Count = 500 },
        }).ToArray();

        CampaignState restored = State();
        restored.Restore(saved);

        IReadOnlyList<CreatureMove> bugbears = restored.MovesOf(Bugbear);
        Assert.Equal(2, bugbears.Count);
        Assert.Equal(Workshop, bugbears[0].ListedInAreaNodeId);
        Assert.Equal(EncounterCount.MaxCount, bugbears[1].Count);
        Assert.Equal(Vestry, bugbears[1].ListedInAreaNodeId);
        Assert.Equal(1, CreatureStanding.Of(Den, Grask.Moved(1), restored).Left);
    }

    [Fact]
    public void The_moves_handed_out_are_copies()
    {
        CampaignState campaign = State();
        campaign.Move(Moved(Grask, Workshop, Den), 0, oneSomebody: true);

        Assert.Single(campaign.MovesOf(Grask.NodeId)).ToAreaNodeId = Hall;
        Assert.Single(campaign.Capture().Moves).ToAreaNodeId = Hall;

        Assert.Equal(Den, Assert.Single(campaign.MovesOf(Grask.NodeId)).ToAreaNodeId);
    }

    private static CreatureMove Moved(EncounterMonster row, string from, string to, int count = 1) => new CreatureMove
    {
        CreatureId = row.NodeId,
        FromAreaNodeId = from,
        ToAreaNodeId = to,
        Count = count,
    };

    private static EncounterMonster Bandits(int count) => new EncounterMonster
    {
        NodeId = "bandit-gang", StatBlockNodeId = "data_statblocks_monsters_bandit", Name = "Gang bandit",
        SuggestedCount = count, Shared = true, BandSize = 7,
    };

    private static CampaignState State() => new CampaignState(new FakePartyRoster());
}
