using System.Collections.Generic;
using System.Linq;
using DungeonTable.Core.Battle;
using DungeonTable.Core.Dossier;
using DungeonTable.Core.Session;
using DungeonTable.Tests.Battles;
using DungeonTable.Web.Services;

namespace DungeonTable.Tests.Quests;

/// <summary>
/// Verifies the campaign singleton behind the quest log: ticking beats, quest status, prerequisite
/// evaluation against the party roster, drawing from decks by their own rule, and the
/// capture/restore pair the persistence coordinator drives.
/// </summary>
public sealed class CampaignStateTests
{
    private const string MillersSon = "the-millers-son";
    private const string Hymnal = "the-lost-hymnal";
    private const string SilentBell = "the-silent-bell";

    // The same beat id twice, as a hand-edited document could easily contain.
    private static readonly string[] RepeatedBeatIds = { "hear-wenna-out", "hear-wenna-out" };

    // Two areas and two creatures for the per-area checklist. The same creature appears in both
    // areas on purpose: a tick belongs to a room, not to a stat block.
    private const string Landing = "data_dossiers_level_1_area_1";
    private const string Den = "data_dossiers_level_1_area_2";
    private const string Bugbear = "data_statblocks_monsters_bugbear";
    private const string Grell = "data_statblocks_monsters_grell";

    private static readonly string[] Roster = { Bugbear, Grell };

    // Two decks, one per draw rule.
    private const string Rumours = "village-rumours";
    private const string Omens = "omens";

    private static readonly string[] RepeatedDeckRestored = { "the-millers-debt", "the-sextons-key" };

    // What a hand-edited document's second entry for the same deck, and a nameless deck, hold.
    private static readonly string[] SecondEntryForRumours = { "the-sextons-key", "the-millers-debt" };
    private static readonly string[] OrphanCard = { "orphan" };

    [Fact]
    public void An_untouched_quest_has_no_status_and_no_ticked_beats()
    {
        CampaignState campaign = State();

        Assert.Equal(QuestStatus.None, campaign.Status(MillersSon));
        Assert.False(campaign.IsBeatDone(MillersSon, "hear-wenna-out"));
        Assert.Empty(campaign.Capture().Quests);
    }

    [Fact]
    public void Ticking_a_beat_toggles_it_and_raises_changed()
    {
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.ToggleBeat(MillersSon, "hear-wenna-out");
        Assert.True(campaign.IsBeatDone(MillersSon, "hear-wenna-out"));

        campaign.ToggleBeat(MillersSon, "hear-wenna-out");
        Assert.False(campaign.IsBeatDone(MillersSon, "hear-wenna-out"));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void The_first_tick_makes_an_untouched_quest_active()
    {
        CampaignState campaign = State();

        campaign.ToggleBeat(MillersSon, "hear-wenna-out");

        Assert.Equal(QuestStatus.Active, campaign.Status(MillersSon));
    }

    [Fact]
    public void A_later_tick_does_not_undo_a_status_the_dm_set()
    {
        // Completing a quest and then ticking a beat the DM had missed must not knock it back to
        // Active — the DM's own call outranks the inference.
        CampaignState campaign = State();
        campaign.SetStatus(MillersSon, QuestStatus.Complete);

        campaign.ToggleBeat(MillersSon, "find-tam");

        Assert.Equal(QuestStatus.Complete, campaign.Status(MillersSon));
    }

    [Fact]
    public void Completed_beats_are_counted_against_the_authored_quest()
    {
        // Ids that are no longer in the authored data are ignored rather than inflating the count.
        CampaignState campaign = State();
        campaign.ToggleBeat(MillersSon, "hear-wenna-out");
        campaign.ToggleBeat(MillersSon, "find-tam");
        campaign.ToggleBeat(MillersSon, "a-beat-that-was-renamed");

        Assert.Equal(2, campaign.CompletedBeats(Quest(MillersSon, "hear-wenna-out", "find-tam", "bring-tam-home")));
    }

    [Fact]
    public void A_quest_prerequisite_is_met_only_once_an_alternative_is_complete()
    {
        CampaignState campaign = State();
        var prerequisite = new QuestPrerequisite
        {
            Kind = QuestPrerequisiteKind.QuestComplete,
            AnyOfQuestIds = new[] { SilentBell, Hymnal },
            Text = "Prerequisite: Complete either quest",
        };

        Assert.False(campaign.IsMet(prerequisite));

        campaign.SetStatus(SilentBell, QuestStatus.Active);
        Assert.False(campaign.IsMet(prerequisite));

        campaign.SetStatus(SilentBell, QuestStatus.Complete);
        Assert.True(campaign.IsMet(prerequisite));
    }

    [Fact]
    public void A_character_level_prerequisite_reads_the_rosters_highest_level()
    {
        var roster = new FakePartyRoster();
        roster.Members.Add(new PartyMember { Name = "Finway", Level = 5 });
        roster.Members.Add(new PartyMember { Name = "Rurik", Level = 9 });
        var campaign = new CampaignState(roster);

        Assert.Equal(9, campaign.PartyLevel());
        Assert.True(campaign.IsMet(Level(9)));
        Assert.False(campaign.IsMet(Level(12)));
    }

    [Fact]
    public void A_roster_with_no_levels_reports_a_level_prerequisite_as_unmet_rather_than_guessing()
    {
        // Levels live on the players' character sheets. Reporting "unmet" shows the DM the
        // requirement text and leaves the decision with them; every beat stays tickable either way.
        var roster = new FakePartyRoster();
        roster.Members.Add(new PartyMember { Name = "Finway" });
        var campaign = new CampaignState(roster);

        Assert.Equal(0, campaign.PartyLevel());
        Assert.False(campaign.IsMet(Level(9)));
    }

    [Fact]
    public void A_quest_with_no_prerequisites_is_always_offerable()
    {
        CampaignState campaign = State();

        Assert.True(campaign.IsOfferable(new Quest { Id = MillersSon }));
        Assert.False(campaign.IsOfferable(new Quest
        {
            Id = Hymnal,
            Prerequisites = new[]
            {
                new QuestPrerequisite
                {
                    Kind = QuestPrerequisiteKind.QuestComplete,
                    AnyOfQuestIds = new[] { MillersSon },
                    Text = "Prerequisite: ...",
                },
            },
        }));
    }

    // ---- Decks: what a draw does is the deck's own rule --------------------------------------

    [Fact]
    public void A_kept_deck_deals_each_card_once_until_it_is_reset()
    {
        CampaignState campaign = State();
        CardDeck rumours = Deck(Rumours, DeckDrawRule.Kept, "the-millers-debt", "the-sextons-key");
        int changes = 0;
        campaign.Changed += () => changes++;

        DeckCard first = campaign.Draw(rumours);
        DeckCard second = campaign.Draw(rumours);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(campaign.IsCardDrawn(Rumours, first.Id));
        Assert.Equal(new[] { first.Id, second.Id }, campaign.DrawnCardIds(Rumours));
        Assert.Empty(campaign.Undrawn(rumours));
        Assert.Equal(string.Empty, campaign.Draw(rumours).Id);
        Assert.Equal(2, changes);

        campaign.ResetDeck(Rumours);

        Assert.Equal(2, campaign.Undrawn(rumours).Count);
        Assert.False(campaign.IsCardDrawn(Rumours, first.Id));
        Assert.Empty(campaign.DrawnCardIds(Rumours));
        Assert.Equal(3, changes);
    }

    [Fact]
    public void A_fresh_deck_remembers_nothing_it_draws()
    {
        // An omen read at every threshold goes straight back in: striking it off would make the
        // deck run out, which the rule says it never does.
        CampaignState campaign = State();
        CardDeck omens = Deck(Omens, DeckDrawRule.Fresh, "cracked-bell", "silent-rope");
        int changes = 0;
        campaign.Changed += () => changes++;

        for (int draw = 0; draw < 5; draw++)
        {
            Assert.NotEqual(string.Empty, campaign.Draw(omens).Id);
        }

        Assert.Equal(2, campaign.Undrawn(omens).Count);
        Assert.Empty(campaign.DrawnCardIds(Omens));
        Assert.Empty(campaign.Capture().Decks);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_deck_that_names_no_rule_is_drawn_fresh()
    {
        // Unset is read as the rule that can never withhold a card by mistake.
        CampaignState campaign = State();
        CardDeck deck = Deck(Omens, DeckDrawRule.None, "cracked-bell");

        campaign.Draw(deck);
        campaign.Draw(deck);

        Assert.Single(campaign.Undrawn(deck));
        Assert.False(campaign.IsCardDrawn(Omens, "cracked-bell"));
    }

    [Fact]
    public void A_kept_deck_with_no_id_has_nowhere_to_keep_its_cards_so_it_draws_fresh()
    {
        // The store never hands one out, but a drawn card recorded under "" would belong to every
        // nameless deck at once.
        CampaignState campaign = State();
        CardDeck nameless = Deck("  ", DeckDrawRule.Kept, "only-card");

        Assert.Equal("only-card", campaign.Draw(nameless).Id);
        Assert.Equal("only-card", campaign.Draw(nameless).Id);
        Assert.Empty(campaign.Capture().Decks);
    }

    [Fact]
    public void Each_kept_deck_keeps_its_own_drawn_cards()
    {
        // Card ids are unique within a deck, not across decks: two decks may both have a "first".
        CampaignState campaign = State();
        CardDeck rumours = Deck(Rumours, DeckDrawRule.Kept, "first");
        CardDeck letters = Deck("letters", DeckDrawRule.Kept, "first");

        campaign.Draw(rumours);

        Assert.True(campaign.IsCardDrawn(Rumours, "first"));
        Assert.False(campaign.IsCardDrawn("letters", "first"));
        Assert.Single(campaign.Undrawn(letters));

        campaign.ResetDeck("letters");
        Assert.True(campaign.IsCardDrawn(Rumours, "first"));
    }

    [Fact]
    public void A_draw_takes_the_card_the_random_source_picks_from_what_is_left()
    {
        CampaignState campaign = State();
        CardDeck rumours = Deck(Rumours, DeckDrawRule.Kept, "a", "b", "c");

        Assert.Equal("b", campaign.Draw(rumours, new FixedRandom(1)).Id);

        // "b" is out, so the same pick lands on what now sits second.
        Assert.Equal("c", campaign.Draw(rumours, new FixedRandom(1)).Id);
        Assert.Equal("a", Assert.Single(campaign.Undrawn(rumours)).Id);
    }

    [Fact]
    public void Resetting_a_deck_with_nothing_drawn_raises_nothing()
    {
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.ResetDeck(Rumours);
        campaign.ResetDeck(null);

        Assert.Equal(0, changes);
    }

    [Fact]
    public void A_null_deck_draws_nothing()
    {
        CampaignState campaign = State();

        Assert.Equal(string.Empty, campaign.Draw(null).Id);
        Assert.Empty(campaign.Undrawn(null));
    }

    [Fact]
    public void Resetting_a_quest_clears_its_status_and_every_tick()
    {
        CampaignState campaign = State();
        campaign.ToggleBeat(MillersSon, "hear-wenna-out");
        campaign.SetStatus(MillersSon, QuestStatus.Complete);

        campaign.ResetQuest(MillersSon);

        Assert.Equal(QuestStatus.None, campaign.Status(MillersSon));
        Assert.False(campaign.IsBeatDone(MillersSon, "hear-wenna-out"));
        Assert.Empty(campaign.Capture().Quests);
    }

    [Fact]
    public void A_captured_campaign_restores_whole()
    {
        CampaignState before = State();
        before.ToggleBeat(MillersSon, "hear-wenna-out");
        before.ToggleBeat(MillersSon, "find-tam");
        before.SetStatus(SilentBell, QuestStatus.Complete);
        before.Draw(Deck(Rumours, DeckDrawRule.Kept, "the-millers-debt"));

        CampaignState after = State();
        after.Restore(before.Capture());

        Assert.Equal(QuestStatus.Active, after.Status(MillersSon));
        Assert.Equal(QuestStatus.Complete, after.Status(SilentBell));
        Assert.True(after.IsBeatDone(MillersSon, "find-tam"));
        Assert.True(after.IsCardDrawn(Rumours, "the-millers-debt"));
    }

    [Fact]
    public void Restoring_replaces_whatever_was_held()
    {
        CampaignState campaign = State();
        campaign.ToggleBeat(Hymnal, "search-the-undercroft");

        campaign.Restore(new CampaignSnapshot());

        Assert.Equal(QuestStatus.None, campaign.Status(Hymnal));
        Assert.Empty(campaign.Capture().Quests);
    }

    [Fact]
    public void A_hand_edited_document_cannot_double_count_a_beat_or_restore_a_nameless_quest()
    {
        CampaignState campaign = State();

        campaign.Restore(new CampaignSnapshot
        {
            Quests = new[]
            {
                new QuestProgress
                {
                    QuestId = MillersSon,
                    Status = QuestStatus.Active,
                    CompletedBeatIds = RepeatedBeatIds,
                },
                new QuestProgress { QuestId = string.Empty, Status = QuestStatus.Complete },
            },
            Decks = new[]
            {
                new DeckProgress { DeckId = Rumours, DrawnCardIds = new[] { "the-millers-debt", string.Empty, "the-millers-debt" } },
                new DeckProgress { DeckId = string.Empty, DrawnCardIds = OrphanCard },
                new DeckProgress { DeckId = Rumours, DrawnCardIds = SecondEntryForRumours },
                new DeckProgress { DeckId = Omens, DrawnCardIds = new[] { string.Empty } },
            },
        });

        Assert.Equal(1, campaign.CompletedBeats(Quest(MillersSon, "hear-wenna-out", "find-tam")));
        Assert.Single(campaign.Capture().Quests);

        // A deck listed twice keeps what either entry drew, once each, in the order first drawn; a
        // nameless deck and a deck with nothing real drawn are not restored at all.
        Assert.Equal(RepeatedDeckRestored, campaign.DrawnCardIds(Rumours));
        Assert.Equal(Rumours, Assert.Single(campaign.Capture().Decks).DeckId);
    }

    [Fact]
    public void A_null_snapshot_is_ignored()
    {
        CampaignState campaign = State();
        campaign.ToggleBeat(MillersSon, "hear-wenna-out");

        campaign.Restore(null);

        Assert.True(campaign.IsBeatDone(MillersSon, "hear-wenna-out"));
    }

    // ---- The area checklist: what has been dealt with, and the DM's own note -------------------

    [Fact]
    public void An_untouched_area_records_nothing_at_all()
    {
        CampaignState campaign = State();

        Assert.False(campaign.IsCreatureCleared(Landing, Bugbear));
        Assert.Equal(string.Empty, campaign.NoteFor(Landing));
        Assert.Empty(campaign.Capture().Areas);
    }

    [Fact]
    public void Ticking_a_creature_off_toggles_it_per_area()
    {
        // Per area, not per creature: the same bugbear stat block appears in a dozen rooms, and
        // killing the ones in area 1 says nothing about the ones in area 28.
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.ToggleCreatureCleared(Landing, Bugbear);

        Assert.True(campaign.IsCreatureCleared(Landing, Bugbear));
        Assert.False(campaign.IsCreatureCleared(Den, Bugbear));

        campaign.ToggleCreatureCleared(Landing, Bugbear);
        Assert.False(campaign.IsCreatureCleared(Landing, Bugbear));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Marking_the_area_cleared_ticks_everything_it_names_and_reopening_puts_it_back()
    {
        CampaignState campaign = State();

        campaign.SetAreaCleared(Landing, Roster, cleared: true);
        Assert.Equal(2, campaign.ClearedCount(Landing, Roster));

        campaign.SetAreaCleared(Landing, Roster, cleared: false);
        Assert.Equal(0, campaign.ClearedCount(Landing, Roster));
    }

    [Fact]
    public void Reopening_an_area_leaves_a_tick_for_a_creature_it_no_longer_names()
    {
        // Re-scanning the prose can drop a creature from an area's encounter. A fight that happened
        // still happened, so the record of it survives being unable to display it.
        CampaignState campaign = State();
        campaign.ToggleCreatureCleared(Landing, Bugbear);
        campaign.ToggleCreatureCleared(Landing, Grell);

        campaign.SetAreaCleared(Landing, new[] { Bugbear }, cleared: false);

        Assert.False(campaign.IsCreatureCleared(Landing, Bugbear));
        Assert.True(campaign.IsCreatureCleared(Landing, Grell));
    }

    [Fact]
    public void The_cleared_count_only_counts_creatures_the_area_still_names()
    {
        // Otherwise a leftover tick could make an area read "3 of 2 dealt with" — or, worse, read as
        // cleared while something is still standing in it.
        CampaignState campaign = State();
        campaign.ToggleCreatureCleared(Landing, Grell);

        Assert.Equal(0, campaign.ClearedCount(Landing, new[] { Bugbear }));
    }

    [Fact]
    public void A_note_is_trimmed_kept_and_deleted_by_blanking_it()
    {
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.SetNote(Landing, "  They spiked the door open.  ");
        Assert.Equal("They spiked the door open.", campaign.NoteFor(Landing));

        // Saved on blur, so tabbing away without typing must not wake the persistence debounce.
        campaign.SetNote(Landing, "They spiked the door open.");
        Assert.Equal(1, changes);

        campaign.SetNote(Landing, "   ");
        Assert.Equal(string.Empty, campaign.NoteFor(Landing));
        Assert.Empty(campaign.Capture().Areas);
    }

    [Fact]
    public void An_area_left_with_no_ticks_and_no_note_stops_being_stored()
    {
        // Otherwise every room the DM ever glanced at would accumulate an empty row in the document.
        CampaignState campaign = State();
        campaign.ToggleCreatureCleared(Landing, Bugbear);
        campaign.SetNote(Landing, "later");

        campaign.SetNote(Landing, string.Empty);
        campaign.ToggleCreatureCleared(Landing, Bugbear);

        Assert.Empty(campaign.Capture().Areas);
    }

    [Fact]
    public void Area_progress_survives_a_capture_and_restore()
    {
        CampaignState campaign = State();
        campaign.ToggleCreatureCleared(Landing, Bugbear);
        campaign.SetNote(Landing, "Doppelganger still at large.");

        CampaignState restored = State();
        restored.Restore(campaign.Capture());

        Assert.True(restored.IsCreatureCleared(Landing, Bugbear));
        Assert.Equal("Doppelganger still at large.", restored.NoteFor(Landing));
    }

    [Fact]
    public void A_restored_area_with_a_repeated_creature_id_is_only_counted_once()
    {
        CampaignState campaign = State();

        campaign.Restore(new CampaignSnapshot
        {
            Areas = new[]
            {
                new AreaProgress { AreaNodeId = Landing, ClearedCreatureIds = new[] { Bugbear, Bugbear } },
            },
        });

        Assert.Equal(1, campaign.ClearedCount(Landing, Roster));
    }

    [Fact]
    public void A_restore_replaces_the_areas_it_does_not_merge_them()
    {
        CampaignState campaign = State();
        campaign.SetNote(Den, "from a different table");

        campaign.Restore(new CampaignSnapshot());

        Assert.Equal(string.Empty, campaign.NoteFor(Den));
    }

    [Fact]
    public void An_area_or_creature_with_no_id_is_ignored()
    {
        CampaignState campaign = State();
        int changes = 0;
        campaign.Changed += () => changes++;

        campaign.ToggleCreatureCleared(string.Empty, Bugbear);
        campaign.ToggleCreatureCleared(Landing, "   ");
        campaign.SetNote(string.Empty, "nowhere");
        campaign.SetAreaCleared(string.Empty, Roster, cleared: true);

        Assert.Empty(campaign.Capture().Areas);
        Assert.Equal(0, changes);
    }

    private static CampaignState State() => new CampaignState(new FakePartyRoster());

    private static CardDeck Deck(string id, DeckDrawRule rule, params string[] cardIds) => new CardDeck
    {
        Id = id,
        DrawRule = rule,
        Cards = cardIds.Select(cardId => new DeckCard { Id = cardId, Name = cardId }).ToArray(),
    };

    private static QuestPrerequisite Level(int level) => new QuestPrerequisite
    {
        Kind = QuestPrerequisiteKind.CharacterLevel,
        CharacterLevel = level,
        Text = $"Prerequisite: {level}th level or higher",
    };

    private static Quest Quest(string id, params string[] beatIds) => new Quest
    {
        Id = id,
        Beats = beatIds.Select((beatId, index) => new QuestBeat
        {
            Id = beatId,
            Ordinal = index + 1,
            Summary = beatId,
        }).ToList(),
    };

    // Picks the same index every time, so a test can say which card a draw lands on.
    private sealed class FixedRandom(int index) : Random
    {
        public override int Next(int maxValue) => index;
    }
}
